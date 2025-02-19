using DocumentChunker.Chunkers;
using HtmlAgilityPack;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO; // Added for File operations

using DocumentChunker.Core;
using Moq.Protected;
using Xunit;
using Shouldly;
using Xunit.Abstractions;

namespace DocumentChunker.Tests
{
    public class HtmlChunkerTests
    {

        private readonly Mock<IChunkerConfig> _mockConfig;
        private HtmlChunker _chunker;
        private ITestOutputHelper Console;
        public HtmlChunkerTests(ITestOutputHelper output) //Inject ITestOutputHelper to get test output in xUnit 
        {
            this.Console = output;
            _mockConfig = new Mock<IChunkerConfig>();
            _mockConfig.Setup(c => c.MaxWordsPerChunk).Returns(10);
            _chunker = new HtmlChunker(_mockConfig.Object);
        }

        [Fact]
        public async Task ExtractChunksAsync_EmptyHtml_ReturnsEmptyList()
        {
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, "");
            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.ShouldBeEmpty();
            }
            finally
            {
                File.Delete(tempFilePath); // Clean up the temporary file
            }
        }


        [Fact]
        public async Task ExtractChunksAsync_SimpleText_ReturnsSingleChunk()
        {
            var html = "This is a simple text.";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBe("This is a simple text.");
            }
            finally
            {
                File.Delete(tempFilePath); // Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_TextExceedsMaxWords_ReturnsMultipleChunks()
        {
            _mockConfig.Setup(c => c.MaxWordsPerChunk).Returns(5);
            var html = "This is a longer text that exceeds the maximum word limit per chunk.";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(3);
                result[0].ShouldBe("This is a longer text");
                result[1].ShouldBe("that exceeds the maximum word");
                result[2].ShouldBe("limit per chunk.");
            }
            finally
            {
                File.Delete(tempFilePath); // Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_WithIgnoredTags_IgnoresContent()
        {
            var html = "<p>This is visible.</p><script>This is ignored.</script><style>Also ignored.</style>";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBe("This is visible.");
            }
            finally
            {
                File.Delete(tempFilePath);  // Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_WithIgnoredClasses_IgnoresContent()
        {
            //Need a custom chunker to set ignored classes
            _mockConfig.Setup(c => c.MaxWordsPerChunk).Returns(10);
            var ignoredClasses = new HashSet<string> { "ignore-me" };

            var chunker = new HtmlChunker(_mockConfig.Object, ignoredClasses); //pass in the ignored classes
            var html = "<p>This is visible.</p><p class=\"ignore-me\">This is ignored.</p>";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBe("This is visible.");
            }
            finally
            {
                File.Delete(tempFilePath); // Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_WithHtmlEntities_DecodesEntities()
        {
            var html = "<p>This &amp; that.</p>";  // &amp; should be decoded to &
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBe("This & that.");
            }
            finally
            {
                File.Delete(tempFilePath); //Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_WithNestedElements_HandlesCorrectly()
        {
            var html = "<div><p>This is <span>nested</span> text.</p></div>";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBe("This is nested text.");
            }
            finally
            {
                File.Delete(tempFilePath); // Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_WithSectionBreaks_CreatesNewChunks()
        {
            var html = "<p>Paragraph 1.</p><h2>Heading</h2><p>Paragraph 2.</p>";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(3);
                result[0].ShouldBe("Paragraph 1.");
                result[1].ShouldBe("Heading");
                result[2].ShouldBe("Paragraph 2.");
            }
            finally
            {
                File.Delete(tempFilePath); //Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_WithLeadingAndTrailingSpaces_TrimsSpaces()
        {
            var html = "<p>   Trimmed text.   </p>";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBe("Trimmed text.");
            }
            finally
            {
                File.Delete(tempFilePath); // Cleanup
            }
        }

        [Fact]
        public async Task ExtractChunksAsync_LongText_SplitsCorrectly()
        {
            _mockConfig.Setup(c => c.MaxWordsPerChunk).Returns(5);
            var html = "This is a very long text that should be split into chunks of five words each.";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);

            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.Count.ShouldBe(4);
                result[0].ShouldBe("This is a very long");
                result[1].ShouldBe("text that should be split");
                result[2].ShouldBe("into chunks of five words");
                result[3].ShouldBe("each.");
            }
            finally
            {
                File.Delete(tempFilePath); //Cleanup
            }
        }
        [Fact]
        public async Task ExtractChunksInPartsAsync_ReturnsCorrectNumberOfParts()
        {
            _mockConfig.Setup(c => c.MaxWordsPerChunk).Returns(5);
            var html = "This is a longer text that exceeds the maximum word limit per chunk.  And some more text here.";
            var tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);
            try
            {
                var result = await _chunker.ExtractChunksInPartsAsync(tempFilePath, 2).ToListAsync();
                result.Count.ShouldBe(3);
                result[0].Count.ShouldBe(2);
                result[1].Count.ShouldBe(2);
                result[2].Count.ShouldBe(1);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task ExtractChunksInPartsAsync_SingleChunk_ReturnsOnePartWithOneChunk()
        {
            var html = "Short text.";
            var tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);
            try
            {
                var result = await _chunker.ExtractChunksInPartsAsync(tempFilePath, 2).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].Count.ShouldBe(1);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task ExtractChunksInPartsAsync_EmptyHtml_ReturnsSingleEmptyPart()
        {
            var tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, "");
            try
            {
                var result = await _chunker.ExtractChunksInPartsAsync(tempFilePath, 2).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].ShouldBeEmpty();
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task ExtractChunksInPartsAsync_ExactMultipleOfChunkSize_ReturnsCorrectParts()
        {
            _mockConfig.Setup(c => c.MaxWordsPerChunk).Returns(2);
            var html = "One Two Three Four";
            var tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);
            try
            {
                var result = await _chunker.ExtractChunksInPartsAsync(tempFilePath, 2).ToListAsync();
                result.Count.ShouldBe(1);
                result[0].Count.ShouldBe(2);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task ExtractChunksFromUrlAsync_ValidUrl_ReturnsChunks()
        {
            // Arrange: Create a test HTML file or use a publicly accessible URL with predictable content.
            const string testUrl = "https://www.example.com"; // Replace with a URL you control or a static test page
            const string testHtmlContent = "<html><body><p>This is test content.</p></body></html>";

            // Use a DelegatingHandler to intercept the HttpClient request.
            var mockHttpMessageHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(testHtmlContent),
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var chunker = new HtmlChunker(_mockConfig.Object, httpClient); //inject httpclient

            // Act
            var chunks = await chunker.ExtractChunksFromUrlAsync(testUrl).ToListAsync();

            // Assert
            chunks.ShouldNotBeEmpty();
            chunks[0].ShouldBe("This is test content.");
            mockHttpMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == testUrl), ItExpr.IsAny<System.Threading.CancellationToken>()); // Verify that HttpClient was called
        }

        [Fact]
        public async Task ExtractChunksFromUrlAsync_InvalidUrl_ThrowsException()
        {
            const string invalidUrl = "invalid-url";
            var mockHttpMessageHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
                .ThrowsAsync(new HttpRequestException());
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var chunker = new HtmlChunker(_mockConfig.Object, httpClient);

            await Should.ThrowAsync<HttpRequestException>(async () => await chunker.ExtractChunksFromUrlAsync(invalidUrl).ToListAsync());
        }

        [Fact]
        public async Task ExtractChunksInPartsFromUrlAsync_ValidUrl_ReturnsChunksInParts()
        {
            const string testUrl = "https://www.example.com";
            const string testHtmlContent = "<html><body><p>This is test content. It has multiple words.</p></body></html>";
            var mockHttpMessageHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(testHtmlContent),
                });
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var chunker = new HtmlChunker(_mockConfig.Object, httpClient);


            var parts = await chunker.ExtractChunksInPartsFromUrlAsync(testUrl, 2).ToListAsync(); //chunksize 2

            parts.ShouldNotBeEmpty();
            mockHttpMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString() == testUrl), ItExpr.IsAny<System.Threading.CancellationToken>()); // Verify that HttpClient was called
        }

        [Fact]
        public async Task ExtractChunksInPartsFromUrlAsync_InvalidUrl_ThrowsException()
        {
            const string invalidUrl = "invalid-url";
            var mockHttpMessageHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
                .ThrowsAsync(new HttpRequestException());
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var chunker = new HtmlChunker(_mockConfig.Object, httpClient);

            await Should.ThrowAsync<HttpRequestException>(async () => await chunker.ExtractChunksInPartsFromUrlAsync(invalidUrl, 2).ToListAsync());
        }
        [Fact]
        public async Task ExtractChunksAsync_TextWithOnlySpaces_ReturnsEmpty()
        {
            var html = "<p>   </p>";
            // Create a temporary HTML file
            string tempFilePath = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFilePath, html);
            try
            {
                var result = await _chunker.ExtractChunksAsync(tempFilePath).ToListAsync();
                result.ShouldBeEmpty();
            }
            finally { File.Delete(tempFilePath); }
        }

    }


    // Abstract class for mocking HttpClient
    public abstract class TestHttpMessageHandler : HttpMessageHandler
    {

    }

    //Helper extension to create HtmlChunker with IgnoredClasses
    public static class HtmlChunkerExtensions
    {
        public static HtmlChunker CreateWithIgnoredClasses(this HtmlChunker chunker, IChunkerConfig config, HashSet<string> ignoredClasses)
        {
            return new HtmlChunker(
                config.MaxWordsPerChunk, true, htmlTagsToExclude: ignoredClasses.ToList());//config, ignoredClasses);
        }
    }

}