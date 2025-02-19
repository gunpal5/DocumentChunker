using Xunit;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Moq;
using Moq.Protected;
using System.Threading;
using System.Net;
using System;
using DocumentChunker.Chunkers;
using DocumentChunker.Core;
using DocumentChunker.Enum;

namespace DocumentChunker.Tests;

public class PlainTextDocumentChunker_Tests
{
    private string CreateTestFile(string content)
    {
        string tempFilePath = Path.GetTempFileName();
        File.WriteAllText(tempFilePath, content);
        return tempFilePath;
    }

    [Fact]
    public async Task ExtractChunksAsync_Paragraph_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "Paragraph 1.\n\nParagraph 2 with more words.\n\n  \n\nP3"; // Include extra blank lines
        string filePath = CreateTestFile(content);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("Paragraph 1.");
        chunks[1].ShouldBe("Paragraph 2 with more words.");
        chunks[2].ShouldBe("P3");
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_Paragraph_ChunksCorrectly_MaxWords()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "Paragraph 1 has many words.\n\nParagraph 2."; //
        string filePath = CreateTestFile(content);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("Paragraph 1 has many words.");
        chunks[1].ShouldBe("Paragraph 2.");
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_Sentence_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Sentence);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "This is sentence one. And this is the second sentence.\nShort.";
        string filePath = CreateTestFile(content);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("This is sentence one.");
        chunks[1].ShouldBe("And this is the second sentence.");
     
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_Sentence_ChunksCorrectly_MaxWords()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 4, chunkType: ChunkType.Sentence);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "This is a long sentence. Short.";
        string filePath = CreateTestFile(content);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("This is a long sentence.");
        chunks[1].ShouldBe("Short.");
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_Word_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "one two three four five";
        string filePath = CreateTestFile(content);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("one two three");
        chunks[1].ShouldBe("four five");
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_Word_ChunksCorrectly_WithNewLine()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "one two three\nfour five";
        string filePath = CreateTestFile(content);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("one two three");
        chunks[1].ShouldBe("four five");
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 10, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string filePath = CreateTestFile(""); // Empty file

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldBeEmpty();
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksInPartsAsync_Paragraph_ChunksInPartsCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "P1\n\nP2\n\nP3\n\nP4";
        string filePath = CreateTestFile(content);
        int chunkSize = 2;

        // Act
        var parts = await chunker.ExtractChunksInPartsAsync(filePath, chunkSize).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(2);
        parts[0].Count.ShouldBe(2);
        parts[0][0].ShouldBe("P1");
        parts[0][1].ShouldBe("P2");
        parts[1].Count.ShouldBe(2);
        parts[1][0].ShouldBe("P3");
        parts[1][1].ShouldBe("P4");
        File.Delete(filePath);
    }

   

    [Fact]
    public async Task ExtractChunksInPartsAsync_InvalidChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "P1\n\nP2";
        string filePath = CreateTestFile(content);
        int chunkSize = 0; // Invalid chunk size

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>( async () =>
            await chunker.ExtractChunksInPartsAsync(filePath, chunkSize).ToListAsync());
        File.Delete(filePath);
    }

   

    [Fact]
    public async Task ExtractChunksInPartsAsync_Word_ChunksInPartsCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Word);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "one two three four five";
        string filePath = CreateTestFile(content);

        int chunkSize = 2; // Request chunks of 2

        // Act
        var parts = await chunker.ExtractChunksInPartsAsync(filePath, chunkSize).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(2); // Expecting two lists
        parts[0].Count.ShouldBe(2);
        parts[0][0].ShouldBe("one two");
        parts[0][1].ShouldBe("three four");
        parts[1].Count.ShouldBe(1);
        parts[1][0].ShouldBe("five");
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksFromUrlAsync_Paragraph_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        string content = "Paragraph 1 from URL.\n\nParagraph 2 from URL.";

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content), // Use StringContent for plain text
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PlainTextDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.txt";

        // Act
        var chunks = await chunker.ExtractChunksFromUrlAsync(fileUrl).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("Paragraph 1 from URL.");
        chunks[1].ShouldBe("Paragraph 2 from URL.");
    }

    [Fact]
    public async Task ExtractChunksFromUrlAsync_HandlesHttpError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound, // Simulate a 404 error
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PlainTextDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.txt";

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await chunker.ExtractChunksFromUrlAsync(fileUrl).ToListAsync());
    }

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_Paragraph_ChunksInPartsCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        string content = "P1 URL\n\nP2 URL\n\nP3 URL\n\nP4 URL";
        int chunkSize = 2;

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content), // Use StringContent
            });
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var chunker = new PlainTextDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.txt";

        // Act
        var parts = await chunker.ExtractChunksInPartsFromUrlAsync(fileUrl, chunkSize).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(2);
        parts[0].Count.ShouldBe(2);
        parts[0][0].ShouldBe("P1 URL");
        parts[0][1].ShouldBe("P2 URL");
        parts[1].Count.ShouldBe(2);
        parts[1][0].ShouldBe("P3 URL");
        parts[1][1].ShouldBe("P4 URL");
    }

    

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_InvalidChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        string content = "P1 URL\n\nP2 URL";
        int chunkSize = 0; // Invalid

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content),
            });
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var chunker = new PlainTextDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.txt";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await chunker.ExtractChunksInPartsFromUrlAsync(fileUrl, chunkSize).ToListAsync());
    }

   

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_Word_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        string content = "one two three four five six seven";
        int chunkSize = 2; // Request chunks of 2

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var chunker = new PlainTextDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.txt";


        // Act
        var parts = await chunker.ExtractChunksInPartsFromUrlAsync(fileUrl, chunkSize).ToListAsync();

        // Assert
        parts.ShouldNotBeNull();
        parts.Count.ShouldBe(2); //Two parts
        parts[0].Count.ShouldBe(2);
        parts[0][0].ShouldBe("one two three");
        parts[0][1].ShouldBe("four five six");
        parts[1].Count.ShouldBe(1);
        parts[1][0].ShouldBe("seven");
    }

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_HandlesHttpError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound, // Simulate a 404 error
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new PlainTextDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.txt";

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await chunker.ExtractChunksInPartsFromUrlAsync(fileUrl, 2).ToListAsync());
    }

    [Fact]
    public async Task ExtractChunksAsync_HandlesFileAccessError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "Paragraph 1. Paragraph 2 with more words. P3";
        string filePath = CreateTestFile(content);

        // Simulate file being locked by another process.
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Act & Assert
            await Assert.ThrowsAnyAsync<IOException>(async () =>
                await chunker.ExtractChunksAsync(filePath).ToListAsync());
        }

        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksInPartsAsync_HandlesFileAccessError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "Paragraph 1. Paragraph 2 with more words. P3";
        string filePath = CreateTestFile(content);
        int chunkSize = 2;

        // Simulate file being locked by another process.
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Act & Assert
            await Assert.ThrowsAnyAsync<IOException>(async () =>
                await chunker.ExtractChunksInPartsAsync(filePath, chunkSize).ToListAsync());
        }

        File.Delete(filePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_InvalidChunkType_ThrowsException()
    {
        // Arrange
        //Invalid chunk type
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: (ChunkType)99);
        var chunker = new PlainTextDocumentChunker(config);
        string content = "This is sentence one. And this is the second sentence. Short.";
        string filePath = CreateTestFile(content);
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await chunker.ExtractChunksAsync(filePath).ToListAsync());
        File.Delete(filePath);
    }
}