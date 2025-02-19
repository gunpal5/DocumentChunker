namespace DocumentChunker.Core;

public interface IDocumentChunker
{
    IAsyncEnumerable<string> ExtractChunksAsync(string documentPath);
    IAsyncEnumerable<string> ExtractChunksFromUrlAsync(string url);
    IAsyncEnumerable<List<string>> ExtractChunksInPartsAsync(string documentPath, int chunkSize);
    IAsyncEnumerable<List<string>> ExtractChunksInPartsFromUrlAsync(string url, int chunkSize);
}