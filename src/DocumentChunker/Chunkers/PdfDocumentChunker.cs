using System.Text;
using System.Text.RegularExpressions;
using DocumentChunker.Core;
using DocumentChunker.Enum;
using UglyToad.PdfPig;

namespace DocumentChunker.Chunkers;

public class PdfDocumentChunker : IDocumentChunker
{
    private readonly IChunkerConfig _config;
    private readonly HttpClient _httpClient;

    public PdfDocumentChunker(IChunkerConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async IAsyncEnumerable<string> ExtractChunksAsync(string documentPath)
    {
        using (var document = PdfDocument.Open(documentPath))
        {
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
        using (var document = PdfDocument.Open(stream))
        {
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

        using (var document = PdfDocument.Open(documentPath))
        {
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
        using (var document = PdfDocument.Open(stream))
        {
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
    private async IAsyncEnumerable<string> ExtractChunksInternal(PdfDocument document)
    {
        switch (_config.ChunkType)
        {
            case ChunkType.Page:
                await foreach (var chunk in ExtractPageChunks(document).ConfigureAwait(false))
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
    private async IAsyncEnumerable<string> ExtractPageChunks(PdfDocument document)
    {
        foreach (var page in document.GetPages())
        {
            //Respect max words per chunk, even for pages.
            var words = page.GetWords().ToList();
            for (int i = 0; i < words.Count; i += _config.MaxWordsPerChunk)
            {
                yield return string.Join(" ", words.Skip(i).Take(_config.MaxWordsPerChunk).Select(w => w.Text));
            }
        }
    }

    private async IAsyncEnumerable<string> ExtractWordChunks(PdfDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;

        foreach (var page in document.GetPages())
        {
            foreach (var word in page.GetWords())
            {
                if (wordCount >= _config.MaxWordsPerChunk)
                {
                    yield return currentChunk.ToString().Trim();
                    currentChunk.Clear();
                    wordCount = 0;
                }

                currentChunk.Append(word.Text);
                currentChunk.Append(" ");
                wordCount++;
            }
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }

    private async IAsyncEnumerable<string> ExtractSentenceChunks(PdfDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;

        foreach (var page in document.GetPages())
        {
            string pageText = page.Text;
            var sentences = Regex.Split(pageText, @"(?<=[\.!\?])\s+"); // Improved sentence splitting

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
                currentChunk.Append(" "); // Add a space (no period, as it's already in the split sentence)
                wordCount += sentenceWordCount;
            }
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }
    private async IAsyncEnumerable<string> ExtractParagraphChunks(PdfDocument document)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;

        foreach (var page in document.GetPages())
        {
            string pageText = page.Text;
            string[] paragraphs = Regex.Split(pageText, @"\r?\n\r?\n+");

            foreach (var paragraph in paragraphs)
            {
                string trimmedParagraph = paragraph.Trim();
                int paragraphWordCount = trimmedParagraph.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                if (wordCount + paragraphWordCount > _config.MaxWordsPerChunk)
                {
                    if (currentChunk.Length > 0)
                    {
                        yield return currentChunk.ToString().Trim();
                    }
                    currentChunk.Clear();
                    wordCount = 0;
                }

                if (trimmedParagraph.Length > 0)
                {
                    currentChunk.Append(trimmedParagraph);
                    currentChunk.Append("\n\n");
                    wordCount += paragraphWordCount;
                }
            }
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }
}