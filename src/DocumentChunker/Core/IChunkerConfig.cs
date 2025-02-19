using DocumentChunker.Enum;

namespace DocumentChunker.Core;

public interface IChunkerConfig
{
    int MaxWordsPerChunk { get; }
    ChunkType ChunkType { get; }
}