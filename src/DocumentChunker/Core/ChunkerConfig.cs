using DocumentChunker.Enum;

namespace DocumentChunker.Core;

public class ChunkerConfig : IChunkerConfig
{
    public int MaxWordsPerChunk { get; }
    public ChunkType ChunkType { get; }

    public ChunkerConfig(int maxWordsPerChunk, ChunkType chunkType)
    {
        MaxWordsPerChunk = maxWordsPerChunk;
        ChunkType = chunkType;
    }
}
