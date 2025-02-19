# Document Chunker Utility Library

This library provides utility classes to break down large files, such as **PDF**, **DOCX**, and **HTML**, into smaller
text chunks for creating corpora for RAG prototyping.

## Purpose

The primary goal of this library is to assist in creating a corpus for prototyping or testing **Retrieval-Augmented
Generation (RAG)** systems. However, the use case of this library should not be limited to this specific purpose. It can
be utilized for any application that requires splitting large text files into manageable pieces.

## Features

- Supports breaking down the following file types:
    - **PDF** files
    - **DOCX** (Microsoft Word) files
    - **HTML** content
- Provides efficient processing for large files.
- Generates precise and context-preserving text chunks.

## Licensing

This library is provided under the **Apache License**. Refer to the repository's [NOTICE](NOTICE) file for information on the
open-source projects leveraged by this library, which are distributed under various permissive open source licenses.

## Usage

### Installation

You can include this library in your .NET project using your preferred method (e.g., `NuGet`, project reference, etc.).

### Example Code

Here's a basic example of how to use the library:

```csharp
// Example usage of the Document Chunker Library

 var config = new ChunkerConfig(maxWordsPerChunk: 11, chunkType: ChunkType.Sentence);
 var chunker = new PdfDocumentChunker(config);
 var filePath = "example.pdf";
 var chunker = new PdfDocumentChunker();
 await foreach (var chunk in chunker.ExtractChunksAsync(testPdfPath))
  {
      Console.WriteLine(chunk);
  }
        
```

## Requirements

- **.NET Framework/SDK versions:**
    - `.Net Frameworkd 4.6.2` and above
    - `.NET 6.0` or above for modern .NET platforms.
    - Compatible with `.NET Standard 2.0` for broader compatibility.

## Contribution

Contributions are welcome! Please feel free to submit issues or pull requests to improve the library.

---

For more details about its features and implementation, check out the [NOTICE](NOTICE) file and [LICENSE](LICENSE) file included
in the project.