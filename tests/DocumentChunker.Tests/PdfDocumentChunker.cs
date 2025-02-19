using System.Net;
using DocumentChunker.Chunkers;
using DocumentChunker.Core;
using DocumentChunker.Enum;
using Moq;
using Moq.Protected;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Shouldly;

namespace DocumentChunker.Tests;

public class PdfDocumentChunkerTests
{
    // Using PDFsharp for PDF generation (MIT License)
    private string CreateTestPdf(List<string> pageContents)
    {
        string tempFilePath = Path.GetTempFileName() + ".pdf";

        // Create a new PDF document
        PdfDocument document = new PdfDocument();

        foreach (var content in pageContents)
        {
            // Add a new page
            PdfPage page = document.AddPage();

            // Get an XGraphics object for drawing on the page
            using (XGraphics gfx = XGraphics.FromPdfPage(page))
            {
                // Create a font
                XFont font = new XFont("Arial", 12, XFontStyleEx.Regular);

                // Draw the string
                gfx.DrawString(content, font, XBrushes.Black,
                    new XRect(50, 50, page.Width - 100, page.Height - 100), //Position and size
                    XStringFormats.TopLeft);
            }
        }

        // Save the document
        document.Save(tempFilePath);
        return tempFilePath;
    }


    [Fact]
    public async Task ExtractChunksAsync_WordChunking_ShouldReturnCorrectChunks()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);
        var chunker = new PdfDocumentChunker(config);
        var pageContent = "This is a test document for chunking."; // 7 words
        var testPdfPath = CreateTestPdf(new List<string> { pageContent });

        // Act
        var chunks = await chunker.ExtractChunksAsync(testPdfPath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("This is a");
        chunks[1].ShouldBe("test document for");
        chunks[2].ShouldBe("chunking.");

        File.Delete(testPdfPath); // Clean up
    }

    [Fact]
    public async Task ExtractChunksAsync_PageChunking_ShouldReturnCorrectChunks()
    {
        // Arrange
        var config =
            new ChunkerConfig(maxWordsPerChunk: 10,
                chunkType: ChunkType.Page); //maxWordsPerChunk is ignored in this case.
        var chunker = new PdfDocumentChunker(config);
        var pageContents = new List<string> { "Page 1 content.", "Page 2 content." };
        var testPdfPath = CreateTestPdf(pageContents);

        // Act
        var chunks = await chunker.ExtractChunksAsync(testPdfPath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("Page 1 content.");
        chunks[1].ShouldBe("Page 2 content.");
        File.Delete(testPdfPath);
    }

    [Fact]
    public async Task ExtractChunksAsync_SentenceChunking_ShouldReturnCorrectChunks()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 11, chunkType: ChunkType.Sentence);
        var chunker = new PdfDocumentChunker(config);
        var pageContent =
            "The quick brown fox jumps over the lazy dog. A journey of a thousand miles begins with a single step? To be or not to be, that is the question!";
        var testPdfPath = CreateTestPdf(new List<string> { pageContent });

        // Act
        var chunks = await chunker.ExtractChunksAsync(testPdfPath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("The quick brown fox jumps over the lazy dog.");
        chunks[1].ShouldBe("A journey of a thousand miles begins with a single step?");
        chunks[2].ShouldBe("To be or not to be, that is the question!");

        File.Delete(testPdfPath);
    }


    [Fact]
    public async Task ExtractChunksFromUrlAsync_ShouldReturnCorrectChunks()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);

        // Mock HttpMessageHandler
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var pageContent = "This is a test document for chunking."; // 7 words, 3 chunks of size 3.

        var pdfBytes = CreatePdfBytes(new List<string> { pageContent });


        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(pdfBytes),
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PdfDocumentChunker(config, httpClient); // Inject HttpClient


        // Act
        var chunks = await chunker.ExtractChunksFromUrlAsync("https://example.com/test.pdf").ToListAsync();


        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("This is a");
        chunks[1].ShouldBe("test document for");
        chunks[2].ShouldBe("chunking.");
    }

    [Fact]
    public async Task ExtractChunksFromUrlAsync_HandlesHttpError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);

        // Mock HttpMessageHandler to return a 404
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound, // 404 Not Found
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PdfDocumentChunker(config, httpClient);

        // Act and Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await chunker.ExtractChunksFromUrlAsync("https://example.com/test.pdf").ToListAsync());
    }

    [Fact]
    public async Task ExtractChunksInPartsAsync_WordChunking_ShouldReturnCorrectParts()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Word);
        var chunker = new PdfDocumentChunker(config);
        var pageContent = "One two three four five six seven eight."; // Eight words.
        var testPdfPath = CreateTestPdf(new List<string> { pageContent });

        // Act
        var parts = await chunker.ExtractChunksInPartsAsync(testPdfPath, 3).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(
            2); // 8 words / (2 words per chunk) = 4 chunks.  4 chunks / (3 chunks per part) = 1 part of 3 and 1 part of 1
        parts[0].Count.ShouldBe(3); // Three chunks in the first part
        parts[0][0].ShouldBe("One two");
        parts[0][1].ShouldBe("three four");
        parts[0][2].ShouldBe("five six");
        parts[1].Count.ShouldBe(1); // One chunk remains
        parts[1][0].ShouldBe("seven eight.");

        File.Delete(testPdfPath); // Clean up
    }


    [Fact]
    public async Task ExtractChunksInPartsAsync_PageChunking_ShouldReturnCorrectParts()
    {
        // Arrange
        var config =
            new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Page); //MaxWordsPerChunk is ignored.
        var chunker = new PdfDocumentChunker(config);
        var pageContents = new List<string> { "Page 1", "Page 2", "Page 3", "Page 4" };
        var testPdfPath = CreateTestPdf(pageContents);

        // Act
        var parts = await chunker.ExtractChunksInPartsAsync(testPdfPath, 2).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(2);
        parts[0].Count.ShouldBe(2);
        parts[0][0].ShouldBe("Page 1");
        parts[0][1].ShouldBe("Page 2");
        parts[1].Count.ShouldBe(2);
        parts[1][0].ShouldBe("Page 3");
        parts[1][1].ShouldBe("Page 4");

        File.Delete(testPdfPath);
    }

    [Fact]
    public async Task ExtractChunksInPartsAsync_ThrowsArgumentExceptionForInvalidChunkSize()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Word);
        var chunker = new PdfDocumentChunker(config);
        var pageContent = "This is a test document."; // 5 words
        var testPdfPath = CreateTestPdf(new List<string> { pageContent });


        // Act & Assert
        await Assert.ThrowsAsync<System.ArgumentException>(async () =>
            await chunker.ExtractChunksInPartsAsync(testPdfPath, 0).ToListAsync()); // Chunk size 0
        await Assert.ThrowsAsync<System.ArgumentException>(async () =>
            await chunker.ExtractChunksInPartsAsync(testPdfPath, -1).ToListAsync()); // Chunk size -1

        File.Delete(testPdfPath);
    }

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_ShouldReturnCorrectParts()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Word);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var pageContent = "One two three four five six seven eight."; // Eight words.

        var pdfBytes = CreatePdfBytes(new List<string> { pageContent });

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(pdfBytes),
            });
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PdfDocumentChunker(config, httpClient);

        // Act
        var parts = await chunker.ExtractChunksInPartsFromUrlAsync("https://example.com/test.pdf", 3).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(
            2); // 8 words / (2 words per chunk) = 4 chunks.  4 chunks / (3 chunks per part) = 1 part of 3 and 1 part of 1
        parts[0].Count.ShouldBe(3); // Three chunks in the first part
        parts[0][0].ShouldBe("One two");
        parts[0][1].ShouldBe("three four");
        parts[0][2].ShouldBe("five six");
        parts[1].Count.ShouldBe(1); // One chunk remains
        parts[1][0].ShouldBe("seven eight.");
    }

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_HandlesHttpError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Word);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound, // 404 Not Found
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PdfDocumentChunker(config, httpClient);
        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await chunker.ExtractChunksInPartsFromUrlAsync("https://example.com/test.pdf", 3).ToListAsync());
    }

    private byte[] CreatePdfBytes(List<string> pageContents)
    {
        using (var memoryStream = new MemoryStream())
        {
            // Create a new PDF document
            PdfDocument document = new PdfDocument();

            foreach (var content in pageContents)
            {
                // Add a new page
                PdfPage page = document.AddPage();

                // Get an XGraphics object for drawing
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    // Create a font
                    XFont font = new XFont("Arial", 12, XFontStyleEx.Regular);

                    // Draw the string
                    gfx.DrawString(content, font, XBrushes.Black,
                        new XRect(50, 50, page.Width - 100, page.Height-100), XStringFormats.TopLeft);
                }
            }

            document.Save(memoryStream);
            return memoryStream.ToArray();
        }
    }
}