using System.Text;
using System.Text.RegularExpressions;
using DocumentChunker.Core;
using DocumentChunker.Enum;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;


namespace DocumentChunker.Chunkers;

public class DocxDocumentChunker : IDocumentChunker
{
    private readonly IChunkerConfig _config;
    private readonly HttpClient _httpClient;

    public DocxDocumentChunker(IChunkerConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async IAsyncEnumerable<string> ExtractChunksAsync(string documentPath)
    {
        using (var document = WordprocessingDocument.Open(documentPath, false))
        {
            if (document.MainDocumentPart?.Document?.Body == null)
            {
                yield break;
            }

            await foreach (var chunk in ExtractChunksInternal(document).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
    }

    public async IAsyncEnumerable<string> ExtractChunksFromUrlAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var document = WordprocessingDocument.Open(stream, false))
        {
            if (document.MainDocumentPart?.Document?.Body == null)
            {
                yield break;
            }

            await foreach (var chunk in ExtractChunksInternal(document).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
    }

    public async IAsyncEnumerable<List<string>> ExtractChunksInPartsAsync(string documentPath, int chunkSize)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));
        }

        using (var document = WordprocessingDocument.Open(documentPath, false))
        {
             if (document.MainDocumentPart?.Document?.Body == null)
            {
                yield break;
            }
            var currentPart = new List<string>();
            await foreach (var chunk in ExtractChunksInternal(document).ConfigureAwait(false))
            {
                currentPart.Add(chunk);
                if (currentPart.Count == chunkSize)
                {
                    yield return currentPart;
                    currentPart = new List<string>();
                }
            }
            if (currentPart.Count > 0)
            {
                yield return currentPart;
            }
        }
    }

    public async IAsyncEnumerable<List<string>> ExtractChunksInPartsFromUrlAsync(string url, int chunkSize)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));
        }

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var document = WordprocessingDocument.Open(stream, false))
        {
            if (document.MainDocumentPart?.Document?.Body == null)
            {
                yield break;
            }
            var currentPart = new List<string>();
            await foreach (var chunk in ExtractChunksInternal(document).ConfigureAwait(false))
            {
                currentPart.Add(chunk);
                if (currentPart.Count == chunkSize)
                {
                    yield return currentPart;
                    currentPart = new List<string>();
                }
            }
            if (currentPart.Count > 0)
            {
                yield return currentPart;
            }
        }
    }
    private async IAsyncEnumerable<string> ExtractChunksInternal(WordprocessingDocument document)
    {
        switch (_config.ChunkType)
        {
            case ChunkType.Page: //Page chunking isn't directly supported for docx. approximating using paragraphs
               await foreach (var chunk in ExtractPageLikeChunks(document).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            case ChunkType.Word:
                await foreach (var chunk in ExtractWordChunks(document).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            case ChunkType.Sentence:
                await foreach (var chunk in ExtractSentenceChunks(document).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            case ChunkType.Paragraph:
                await foreach (var chunk in ExtractParagraphChunks(document).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_config.ChunkType), "Invalid ChunkType");
        }
    }
    private async IAsyncEnumerable<string> ExtractPageLikeChunks(WordprocessingDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;
         // Estimate a page as a certain number of paragraphs or characters. This is *not* precise.
        const int paragraphsPerPageEstimate = 20;  // Adjust this as needed.
        int paragraphCount = 0;

        foreach (var paragraph in document.MainDocumentPart.Document.Body.Descendants<Paragraph>())
        {
            string paragraphText = paragraph.InnerText;
            int paragraphWordCount = paragraphText.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

            if (wordCount + paragraphWordCount > _config.MaxWordsPerChunk || paragraphCount >= paragraphsPerPageEstimate )
            {
                 if (currentChunk.Length > 0)
                {
                    yield return currentChunk.ToString().Trim();
                }
                currentChunk.Clear();
                wordCount = 0;
                paragraphCount = 0;
            }
                currentChunk.Append(paragraphText);
                currentChunk.Append(" ");  // Add space between paragraphs
                wordCount += paragraphWordCount;
                paragraphCount++;

        }
        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }

    private async IAsyncEnumerable<string> ExtractWordChunks(WordprocessingDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;

        foreach (var paragraph in document.MainDocumentPart.Document.Body.Descendants<Paragraph>())
        {
            string paragraphText = paragraph.InnerText;
             string[] words = paragraphText.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if (wordCount >= _config.MaxWordsPerChunk)
                {
                    yield return currentChunk.ToString().Trim();
                    currentChunk.Clear();
                    wordCount = 0;
                }

                currentChunk.Append(word);
                currentChunk.Append(" ");
                wordCount++;
            }
             currentChunk.Append("\n"); //Add paragraph breaks for docx
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }

    private async IAsyncEnumerable<string> ExtractSentenceChunks(WordprocessingDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;

        foreach (var paragraph in document.MainDocumentPart.Document.Body.Descendants<Paragraph>())
        {
            string paragraphText = paragraph.InnerText;
            var sentences = Regex.Split(paragraphText, @"(?<=[\.!\?])\s+");

            foreach (var sentence in sentences)
            {
                int sentenceWordCount = sentence.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                if (wordCount + sentenceWordCount > _config.MaxWordsPerChunk)
                {
                    if (currentChunk.Length > 0)
                    {
                        yield return currentChunk.ToString().Trim();
                        currentChunk.Clear();
                        wordCount = 0;
                    }
                }

                currentChunk.Append(sentence);
                currentChunk.Append(" "); // Add space (no period, as sentence already ends in one)
                wordCount += sentenceWordCount;
            }
            currentChunk.Append("\n"); // Add paragraph breaks for docx
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }


    private async IAsyncEnumerable<string> ExtractParagraphChunks(WordprocessingDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;

        foreach (var paragraph in document.MainDocumentPart.Document.Body.Descendants<Paragraph>())
        {
            string paragraphText = paragraph.InnerText;
             int paragraphWordCount = paragraphText.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;


            if (wordCount + paragraphWordCount > _config.MaxWordsPerChunk)
            {
                if (currentChunk.Length > 0)
                {
                    yield return currentChunk.ToString().Trim();
                    currentChunk.Clear();
                    wordCount = 0;
                }
            }
              if (!string.IsNullOrWhiteSpace(paragraphText)) //Avoid empty paragraphs.
                {
                    currentChunk.Append(paragraphText);
                    currentChunk.Append("\n"); //Preserve paragraph breaks
                    wordCount += paragraphWordCount;
                }
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }
}