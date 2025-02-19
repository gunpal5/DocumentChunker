using System.Net;
using DocumentChunker.Chunkers;
using DocumentChunker.Core;
using DocumentChunker.Enum;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Moq;
using Moq.Protected;
using Shouldly;

// Test class
namespace DocumentChunker.Tests;

public class DocxDocumentChunkerTests
{
    private string CreateTestDocument(List<string> paragraphs, List<List<string>>? sentences = null,
        List<List<string>>? words = null)
    {
        string tempFilePath = Path.GetTempFileName() + ".docx";

        using (WordprocessingDocument doc =
               WordprocessingDocument.Create(tempFilePath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            if (paragraphs != null)
            {
                foreach (var paragraphText in paragraphs)
                {
                    Paragraph paragraph = new Paragraph();
                    Run run = new Run();
                    Text text = new Text(paragraphText);
                    run.AppendChild(text);
                    paragraph.AppendChild(run);
                    body.AppendChild(paragraph);
                }
            }
            else if (sentences != null)
            {
                foreach (var sentenceGroup in sentences)
                {
                    Paragraph paragraph = new Paragraph();
                    foreach (var sentenceText in sentenceGroup)
                    {
                        Run run = new Run();
                        Text text = new Text(sentenceText);
                        run.AppendChild(text);
                        paragraph.AppendChild(run);
                    }

                    body.AppendChild(paragraph);
                }
            }
            else if (words != null)
            {
                foreach (var wordGroup in words)
                {
                    Paragraph paragraph = new Paragraph();
                    Run run = new Run();
                    foreach (var word in wordGroup)
                    {
                        run.AppendChild(new Text(word + " "));
                    }

                    paragraph.AppendChild(run);
                    body.AppendChild(paragraph);
                }
            }
        }

        return tempFilePath;
    }

    [Fact]
    public async Task ExtractChunksAsync_Paragraph_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Paragraph);
        var chunker = new DocxDocumentChunker(config);
        var paragraphs = new List<string> { "Paragraph 1.", "Paragraph 2 with more words.", "P3" };
        string filePath = CreateTestDocument(paragraphs);

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
    public async Task ExtractChunksAsync_Sentence_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Sentence);
        var chunker = new DocxDocumentChunker(config);
        var sentences = new List<List<string>>
        {
            new List<string> { "This is sentence one. ", "And this is the second sentence." },
            new List<string> { "Short lkshadflkhs alfhlk fhklsd fhklhkl." }
        };
        string filePath = CreateTestDocument(null, sentences);

        // Act
        var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("This is sentence one.");
        chunks[1].ShouldBe("And this is the second sentence.");
        chunks[2].ShouldBe("Short lkshadflkhs alfhlk fhklsd fhklhkl.");
        File.Delete(filePath);
    }

    // [Fact]
    // public async Task ExtractChunksAsync_Sentence_ChunksCorrectly_LongSentence()
    // {
    //     // Arrange
    //     var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Sentence); //Small chunk size
    //     var chunker = new DocxDocumentChunker(config);
    //     var sentences = new List<List<string>>
    //     {
    //         new List<string> { "This is a very long sentence that should be split. ", "Another sentence." },
    //         new List<string> { "Short." }
    //     };
    //     string filePath = CreateTestDocument(null, sentences);
    //
    //     // Act
    //     var chunks = await chunker.ExtractChunksAsync(filePath).ToListAsync();
    //
    //     // Assert
    //     chunks.ShouldNotBeNull();
    //     chunks.Count.ShouldBe(3);
    //     chunks[0].ShouldBe("This is a very long");
    //     chunks[1].ShouldBe("sentence that should be split.");
    //     chunks[2].ShouldBe("Another sentence. Short.");
    //     File.Delete(filePath);
    // }

    [Fact]
    public async Task ExtractChunksAsync_Word_ChunksCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 3, chunkType: ChunkType.Word);
        var chunker = new DocxDocumentChunker(config);
        var words = new List<List<string>>
        {
            new List<string> { "one", "two", "three", "four", "five" }
        };
        string filePath = CreateTestDocument(null, null, words);

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
    public async Task ExtractChunksAsync_EmptyDocument_ReturnsEmptyList()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 10, chunkType: ChunkType.Paragraph);
        var chunker = new DocxDocumentChunker(config);
        string filePath = CreateTestDocument(new List<string>()); // Empty document

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
        var config = new ChunkerConfig(maxWordsPerChunk: 1, chunkType: ChunkType.Paragraph);
        var chunker = new DocxDocumentChunker(config);
        var paragraphs = new List<string> { "P1", "P2", "P3", "P4" };
        string filePath = CreateTestDocument(paragraphs);
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
        var chunker = new DocxDocumentChunker(config);
        var paragraphs = new List<string> { "P1", "P2" };
        string filePath = CreateTestDocument(paragraphs);
        int chunkSize = 0;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await chunker.ExtractChunksInPartsAsync(filePath, chunkSize).ToListAsync());
        File.Delete(filePath);
    }

    

    [Fact]
    public async Task ExtractChunksInPartsAsync_Word_ChunksInPartsCorrectly()
    {
        //Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Word);
        var chunker = new DocxDocumentChunker(config);
        var words = new List<List<string>>
        {
            new List<string> { "one", "two", "three", "four", "five" }
        };

        string filePath = CreateTestDocument(null, null, words);
        int chunkSize = 2;

        //Act
        var parts = await chunker.ExtractChunksInPartsAsync(filePath, chunkSize).ToListAsync();


        //Assert
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
        var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        var paragraphs = new List<string> { "Paragraph 1 from URL.", "Paragraph 2 from URL." };
        string tempFilePath = CreateTestDocument(paragraphs);
        var fileContent = await File.ReadAllBytesAsync(tempFilePath);


        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(fileContent),
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var chunker = new DocxDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.docx";

        // Act
        var chunks = await chunker.ExtractChunksFromUrlAsync(fileUrl).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("Paragraph 1 from URL.");
        chunks[1].ShouldBe("Paragraph 2 from URL.");
        File.Delete(tempFilePath);
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
        var chunker = new DocxDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.docx";

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await chunker.ExtractChunksFromUrlAsync(fileUrl).ToListAsync());
    }


  
    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_InvalidChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var paragraphs = new List<string> { "P1 URL", "P2 URL", "P3 URL", "P4 URL" };
        string tempFilePath = CreateTestDocument(paragraphs);
        var fileContent = await File.ReadAllBytesAsync(tempFilePath);
        int chunkSize = 0;

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(fileContent),
            });
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var chunker = new DocxDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.docx";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await chunker.ExtractChunksInPartsFromUrlAsync(fileUrl, chunkSize).ToListAsync());
        File.Delete(tempFilePath);
    }

    

    [Fact]
    public async Task ExtractChunksInPartsFromUrlAsync_Word_ChunksCorrectly()
    {
        var config = new ChunkerConfig(3, ChunkType.Word); // Max 3 words per chunk
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        var words = new List<List<string>>()
        {
            new List<string> { "one", "two", "three", "four", "five", "six", "seven" }
        };

        string tempFilePath = CreateTestDocument(null, null, words);

        var fileContent = await File.ReadAllBytesAsync(tempFilePath);

        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(fileContent)
            });
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var chunker = new DocxDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.docx";
        int chunkSize = 2; // Request chunks of 2

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

        File.Delete(tempFilePath);
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
        var chunker = new DocxDocumentChunker(config, httpClient);
        string fileUrl = "http://example.com/test.docx";

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await chunker.ExtractChunksInPartsFromUrlAsync(fileUrl, 2).ToListAsync());
    }

    [Fact]
    public async Task ExtractChunksAsync_HandlesFileAccessError()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: ChunkType.Paragraph);
        var chunker = new DocxDocumentChunker(config);
        var paragraphs = new List<string> { "Paragraph 1.", "Paragraph 2 with more words.", "P3" };
        string filePath = CreateTestDocument(paragraphs);

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
        var chunker = new DocxDocumentChunker(config);
        var paragraphs = new List<string> { "Paragraph 1.", "Paragraph 2 with more words.", "P3" };
        string filePath = CreateTestDocument(paragraphs);
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
    public async Task ExtractChunks_ParagraphsWithDifferentFormatting_HandlesFormattingCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 5, chunkType: ChunkType.Paragraph);
        var chunker = new DocxDocumentChunker(config);

        string tempFilePath = Path.GetTempFileName() + ".docx";
        using (WordprocessingDocument doc =
               WordprocessingDocument.Create(tempFilePath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            // Paragraph with bold text
            Paragraph boldParagraph = new Paragraph();
            Run boldRun = new Run();
            RunProperties boldRunProperties = new RunProperties();
            boldRunProperties.AppendChild(new Bold());
            boldRun.AppendChild(boldRunProperties);
            boldRun.AppendChild(new Text("This is bold text. "));
            boldParagraph.AppendChild(boldRun);
            mainPart.Document.Body.AppendChild(boldParagraph);


            // Paragraph with italic text
            Paragraph italicParagraph = new Paragraph();
            Run italicRun = new Run();
            RunProperties italicRunProperties = new RunProperties();
            italicRunProperties.AppendChild(new Italic());
            italicRun.AppendChild(italicRunProperties);
            italicRun.AppendChild(new Text("This is italic text."));
            italicParagraph.AppendChild(italicRun);
            mainPart.Document.Body.AppendChild(italicParagraph);
        }


        // Act
        var chunks = await chunker.ExtractChunksAsync(tempFilePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("This is bold text."); //Formatting tags are not part of the text
        chunks[1].ShouldBe("This is italic text.");
        File.Delete(tempFilePath);
    }

    [Fact]
    public async Task ExtractChunks_SentencesWithDifferentFormatting_HandlesFormattingCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 1, chunkType: ChunkType.Sentence);
        var chunker = new DocxDocumentChunker(config);
        string tempFilePath = Path.GetTempFileName() + ".docx";
        using (WordprocessingDocument doc =
               WordprocessingDocument.Create(tempFilePath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            // Paragraph with mixed formatting
            Paragraph mixedParagraph = new Paragraph();
            Run boldRun = new Run();
            RunProperties boldRunProperties = new RunProperties();
            boldRunProperties.AppendChild(new Bold());
            boldRun.AppendChild(boldRunProperties);
            boldRun.AppendChild(new Text("Bold. "));
            mixedParagraph.AppendChild(boldRun);

            Run italicRun = new Run();
            RunProperties italicRunProperties = new RunProperties();
            italicRunProperties.AppendChild(new Italic());
            italicRun.AppendChild(italicRunProperties);
            italicRun.AppendChild(new Text("Italic. "));
            mixedParagraph.AppendChild(italicRun);

            Run regularRun = new Run();
            regularRun.AppendChild(new Text("Regular."));
            mixedParagraph.AppendChild(regularRun);

            mainPart.Document.Body.AppendChild(mixedParagraph);
        }

        // Act
        var chunks = await chunker.ExtractChunksAsync(tempFilePath).ToListAsync();


        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("Bold.");
        chunks[1].ShouldBe("Italic.");
        chunks[2].ShouldBe("Regular.");
        File.Delete(tempFilePath);
    }

    [Fact]
    public async Task ExtractChunks_WordsWithDifferentFormatting_HandlesFormattingCorrectly()
    {
        // Arrange
        var config = new ChunkerConfig(maxWordsPerChunk: 2, chunkType: ChunkType.Word);
        var chunker = new DocxDocumentChunker(config);

        string tempFilePath = Path.GetTempFileName() + ".docx";
        using (WordprocessingDocument doc =
               WordprocessingDocument.Create(tempFilePath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            // Paragraph with mixed formatting
            Paragraph mixedParagraph = new Paragraph();
            Run boldRun = new Run();
            RunProperties boldRunProperties = new RunProperties();
            boldRunProperties.AppendChild(new Bold());
            boldRun.AppendChild(boldRunProperties);
            boldRun.AppendChild(new Text("BoldWord "));
            mixedParagraph.AppendChild(boldRun);

            Run italicRun = new Run();
            RunProperties italicRunProperties = new RunProperties();
            italicRunProperties.AppendChild(new Italic());
            italicRun.AppendChild(italicRunProperties);
            italicRun.AppendChild(new Text("ItalicWord "));
            mixedParagraph.AppendChild(italicRun);


            Run regularRun = new Run();
            regularRun.AppendChild(new Text("RegularWord "));
            mixedParagraph.AppendChild(regularRun);

            mainPart.Document.Body.AppendChild(mixedParagraph);
        }

        // Act
        var chunks = await chunker.ExtractChunksAsync(tempFilePath).ToListAsync();

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe("BoldWord ItalicWord");
        chunks[1].ShouldBe("RegularWord");
        File.Delete(tempFilePath);
    }

    [Fact]
    public async Task ExtractChunksAsync_InvalidChunkType_ThrowsException()
    {
        // Arrange
        // Invalid chunk type
        var config = new ChunkerConfig(maxWordsPerChunk: 100, chunkType: (ChunkType)99);
        var chunker = new DocxDocumentChunker(config);

        //Dummy file for testing purposes
        var sentences = new List<List<string>>
        {
            new List<string> { "This is sentence one. ", "And this is the second sentence." },
            new List<string> { "Short." }
        };
        string filePath = CreateTestDocument(null, sentences);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await chunker.ExtractChunksAsync(filePath).ToListAsync());
        File.Delete(filePath);
    }
}