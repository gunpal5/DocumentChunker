using DocumentChunker.Core;
using DocumentChunker.Enum;

namespace DocumentChunker.Chunkers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class PlainTextDocumentChunker : IDocumentChunker
{
    private readonly IChunkerConfig _config;
    private readonly HttpClient _httpClient;

    public PlainTextDocumentChunker(IChunkerConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async IAsyncEnumerable<string> ExtractChunksAsync(string documentPath)
    {
        using (var reader = new StreamReader(documentPath))
        {
            await foreach (var chunk in ExtractChunksInternal(reader).ConfigureAwait(false))
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
        using (var reader = new StreamReader(stream))
        {
            await foreach (var chunk in ExtractChunksInternal(reader).ConfigureAwait(false))
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

        using (var reader = new StreamReader(documentPath))
        {
            var currentPart = new List<string>();
            await foreach (var chunk in ExtractChunksInternal(reader).ConfigureAwait(false))
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
        using (var reader = new StreamReader(stream))
        {
            var currentPart = new List<string>();
            await foreach (var chunk in ExtractChunksInternal(reader).ConfigureAwait(false))
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

    private async IAsyncEnumerable<string> ExtractChunksInternal(StreamReader reader)
    {
        switch (_config.ChunkType)
        {
            case ChunkType.Paragraph:
                await foreach (var chunk in ExtractParagraphChunks(reader).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            case ChunkType.Sentence:
                await foreach (var chunk in ExtractSentenceChunks(reader).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            case ChunkType.Word:
                await foreach (var chunk in ExtractWordChunks(reader).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_config.ChunkType), "Invalid ChunkType");
        }
    }
    private async IAsyncEnumerable<string> ExtractParagraphChunks(StreamReader reader)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            string trimmedLine = line.Trim();
            int lineWordCount = trimmedLine.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;


            if (string.IsNullOrWhiteSpace(line)) // Paragraph break
            {
                if (currentChunk.Length > 0)
                {
                    yield return currentChunk.ToString().Trim();
                    currentChunk.Clear();
                    wordCount = 0;
                }
            }
            else
            {

                if (wordCount + lineWordCount > _config.MaxWordsPerChunk)
                {
                    if (currentChunk.Length > 0)
                    {
                        yield return currentChunk.ToString().Trim();
                        currentChunk.Clear();
                        wordCount = 0;
                    }
                }
                 currentChunk.AppendLine(trimmedLine); // Use AppendLine to preserve line breaks within paragraph
                 wordCount += lineWordCount;
            }
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }
    private async IAsyncEnumerable<string> ExtractSentenceChunks(StreamReader reader)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;
        var text = await reader.ReadToEndAsync(); // Read the entire file for sentence splitting
        var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");

        foreach (var sentence in sentences)
        {
            string trimmedSentence = sentence.Trim();
            int sentenceWordCount = trimmedSentence.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;


            if (wordCount + sentenceWordCount > _config.MaxWordsPerChunk)
            {
                if (currentChunk.Length > 0)
                {
                    yield return currentChunk.ToString().Trim();
                    currentChunk.Clear();
                    wordCount = 0;
                }
            }

            if (!string.IsNullOrEmpty(trimmedSentence))
            {
               currentChunk.Append(trimmedSentence);
               currentChunk.Append(" "); // Add a space (no period, as it's already in split)
               wordCount += sentenceWordCount;
            }

        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }
    private async IAsyncEnumerable<string> ExtractWordChunks(StreamReader reader)
    {
        var currentChunk = new StringBuilder();
        int wordCount = 0;
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            var words = line.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().Trim();
        }
    }
}