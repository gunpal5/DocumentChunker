// This file is a .Net port of project https://github.com/google/labs-prototypes/tree/main/seeds/chunker-python
// originally licensed under Apache License 2.0.
// Copyright 2023 Google LLC
// Licensed under the Apache License, Version 2.0
// http://www.apache.org/licenses/LICENSE-2.0
//
// Port made by Gunpal Jain.


using DocumentChunker.Core;
using HtmlAgilityPack;

namespace DocumentChunker.Chunkers;

/// <summary>
/// Chunks HTML documents into text passages.
/// </summary>
public class HtmlChunker : IDocumentChunker
{
    private static readonly HashSet<string> SECTION_BREAK_HTML_TAGS = new(StringComparer.OrdinalIgnoreCase)
    {
        "article", "br", "div", "h1", "h2", "h3", "h4", "h5", "h6",
        "hr", "footer", "header", "main", "nav"
    };

    private static readonly HashSet<string> DEFAULT_HTML_TAGS_TO_EXCLUDE = new(StringComparer.OrdinalIgnoreCase)
    {
        "noscript", "script", "style"
    };

    private static readonly HashSet<string> DEFAULT_HTML_CLASSES_TO_EXCLUDE = new(StringComparer.OrdinalIgnoreCase);

    private readonly int _maxWordsPerAggregatePassage;
    private readonly bool _greedilyAggregateSiblingNodes;
    private readonly HashSet<string> _htmlTagsToExclude;
    private readonly HashSet<string> _htmlClassesToExclude;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlChunker"/> class.
    /// </summary>
    /// <param name="maxWordsPerAggregatePassage">Maximum number of words in a passage that is made up by combining multiple HTML nodes' text.  A passage with text from only a single node may exceed this maximum.</param>
    /// <param name="greedilyAggregateSiblingNodes">If true, sibling HTML nodes are greedily aggregated into a single passage as long as the total word count does not exceed <paramref name="maxWordsPerAggregatePassage"/>.  If false, each sibling node is output as a separate passage (unless they can all be combined).</param>
    /// <param name="htmlTagsToExclude">Text within these tags will not be included in the output passages.</param>
    /// <param name="htmlClassesToExclude">Text within these classes will not be included in the output passages.</param>
    public HtmlChunker(
        int maxWordsPerAggregatePassage,
        bool greedilyAggregateSiblingNodes,
        IEnumerable<string> htmlTagsToExclude = null,
        IEnumerable<string> htmlClassesToExclude = null)
    {
        _maxWordsPerAggregatePassage = maxWordsPerAggregatePassage;
        _greedilyAggregateSiblingNodes = greedilyAggregateSiblingNodes;
        _htmlTagsToExclude = (htmlTagsToExclude != null)
            ? new HashSet<string>(htmlTagsToExclude.Select(tag => tag.Trim().ToLowerInvariant()))
            : new HashSet<string>(DEFAULT_HTML_TAGS_TO_EXCLUDE);

        _htmlClassesToExclude = (htmlClassesToExclude != null)
            ? new HashSet<string>(htmlClassesToExclude.Select(cls => cls.Trim().ToLowerInvariant()))
            : new HashSet<string>(DEFAULT_HTML_CLASSES_TO_EXCLUDE);

        _httpClient = new HttpClient(); 
    }

    public HtmlChunker(IChunkerConfig config, HttpClient? httpClient = null)
    {
        this._maxWordsPerAggregatePassage = config.MaxWordsPerChunk;
        this._httpClient = _httpClient ?? new HttpClient();
        _greedilyAggregateSiblingNodes = true;
        _htmlTagsToExclude = new HashSet<string>(DEFAULT_HTML_TAGS_TO_EXCLUDE);

        _htmlClassesToExclude = new HashSet<string>(DEFAULT_HTML_CLASSES_TO_EXCLUDE);
    }
    
    public HtmlChunker(IChunkerConfig config,  IEnumerable<string> htmlTagsToExclude,
        IEnumerable<string> htmlClassesToExclude = null)
    {
        this._maxWordsPerAggregatePassage = config.MaxWordsPerChunk;
        _htmlTagsToExclude = (htmlTagsToExclude != null)
            ? new HashSet<string>(htmlTagsToExclude.Select(tag => tag.Trim().ToLowerInvariant()))
            : new HashSet<string>(DEFAULT_HTML_TAGS_TO_EXCLUDE);

        _htmlClassesToExclude = (htmlClassesToExclude != null)
            ? new HashSet<string>(htmlClassesToExclude.Select(cls => cls.Trim().ToLowerInvariant()))
            : new HashSet<string>(DEFAULT_HTML_CLASSES_TO_EXCLUDE);
        this._httpClient = _httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Contains aggregate information about a node and its descendants.
    /// </summary>
    private class AggregateNode
    {
        public string HtmlTag { get; set; }
        public List<string> Segments { get; } = new();
        public int NumWords { get; set; }
        public PassageList PassageList { get; } = new();

        /// <summary>
        /// Returns true if this node and the additional node together do not exceed the given max word count.
        /// </summary>
        public bool Fits(AggregateNode other, int maxWords)
        {
            return (NumWords + other.NumWords) <= maxWords;
        }

        /// <summary>
        /// Adds the contents (segments) and word count of the specified node to this AggregateNode.
        /// </summary>
        public void AddNode(AggregateNode other)
        {
            if (other.Segments.Count == 0) return;
            NumWords += other.NumWords;
            Segments.AddRange(other.Segments);
        }

        /// <summary>
        /// Creates and returns a combined text passage from the segments.
        /// </summary>
        public string CreatePassage()
        {
            var filtered = Segments.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            return string.Join(" ", filtered);
        }

        /// <summary>
        /// Returns a list of text passages collected within this node.
        /// </summary>
        public List<string> GetPassages()
        {
            return PassageList.Passages;
        }
    }
    /// <summary>
    /// Represents a list of text passages.
    /// </summary>
    private class PassageList
    {
        public List<string> Passages { get; } = new();

        public void AddPassageForNode(AggregateNode node)
        {
            var passage = node.CreatePassage();
            if (!string.IsNullOrWhiteSpace(passage))
            {
                Passages.Add(passage);
            }
        }

        public void Extend(PassageList other)
        {
            Passages.AddRange(other.Passages);
        }
    }

    /// <summary>
    /// Extracts text chunks from an HTML document loaded from a file.
    /// </summary>
    /// <param name="documentPath">The path to the HTML file.</param>
    /// <returns>An asynchronous enumerable of text chunks.</returns>
    public async IAsyncEnumerable<string> ExtractChunksAsync(string documentPath)
    {
        #if NET462 || NETSTANDARD2_0
        string html = File.ReadAllText(documentPath);
        #else
        
        string html = await File.ReadAllTextAsync(documentPath);
        #endif
        foreach (string chunk in Chunk(html))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Extracts text chunks from an HTML document fetched from a URL.
    /// </summary>
    /// <param name="url">The URL of the HTML document.</param>
    /// <returns>An asynchronous enumerable of text chunks.</returns>
    public async IAsyncEnumerable<string> ExtractChunksFromUrlAsync(string url)
    {
        string html = await _httpClient.GetStringAsync(url);
        foreach (string chunk in Chunk(html))
        {
            yield return chunk;
        }
    }
    
    /// <summary>
    /// Extracts text chunks from an HTML document in parts.
    /// </summary>
    /// <param name="documentPath">The path to the HTML file.</param>
    /// <param name="chunkSize">The number of passage at a time.</param>
    /// <returns>An asynchronous enumerable of list of text chunks.</returns>
    public async IAsyncEnumerable<List<string>> ExtractChunksInPartsAsync(string documentPath, int chunkSize)
    {
#if NET462 || NETSTANDARD2_0
        string html = File.ReadAllText(documentPath);
#else
        
        string html = await File.ReadAllTextAsync(documentPath);
#endif
        List<string> chunks = Chunk(html);

        for (int i = 0; i < chunks.Count; i += chunkSize)
        {
            yield return chunks.Skip(i).Take(chunkSize).ToList();
        }
    }

    /// <summary>
    /// Extracts text chunks from an HTML document fetched from a URL, in parts.
    /// </summary>
    /// <param name="url">The URL of the HTML document.</param>
    /// <param name="chunkSize">The number of passage at a time.</param>
    /// <returns>An asynchronous enumerable of lists of text chunks.</returns>
    public async IAsyncEnumerable<List<string>> ExtractChunksInPartsFromUrlAsync(string url, int chunkSize)
    {
        string html = await _httpClient.GetStringAsync(url);
        List<string> chunks = Chunk(html);
        for (int i = 0; i < chunks.Count; i += chunkSize)
        {
            yield return chunks.Skip(i).Take(chunkSize).ToList();
        }
    }

    /// <summary>
    /// Chunks the HTML into text passages.
    /// </summary>
    /// <param name="html">HTML string.</param>
    /// <returns>List of text passages extracted from HTML.</returns>
    private List<string> Chunk(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rootAggNode = ProcessNode(doc.DocumentNode);

        if (rootAggNode.GetPassages().Count == 0)
        {
            rootAggNode.PassageList.AddPassageForNode(rootAggNode);
        }

        return rootAggNode.GetPassages();
    }

    /// <summary>
    /// Recursively processes a node and its children.
    /// </summary>
    private AggregateNode ProcessNode(HtmlNode node)
    {
        var currentNode = new AggregateNode
        {
            HtmlTag = node.Name
        };

        if (ShouldExcludeNode(node))
        {
            return currentNode;
        }

        if (node.NodeType == HtmlNodeType.Text)
        {
            if (!node.ParentNode.Name.Equals("#document", StringComparison.OrdinalIgnoreCase))
            {
                var text = node.InnerText?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    currentNode.NumWords = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
                    currentNode.Segments.Add(text);
                }
            }
            return currentNode;
        }

        var currentAggregatingNode = new AggregateNode();
        var currentGreedyAggregatingNode = new AggregateNode();
        bool shouldAggregateCurrentNode = true;
        var passageList = new PassageList();

        foreach (var child in node.ChildNodes)
        {
            var childNode = ProcessNode(child);

            if (childNode.GetPassages().Count > 0)
            {
                shouldAggregateCurrentNode = false;
                if (_greedilyAggregateSiblingNodes)
                {
                    passageList.AddPassageForNode(currentGreedyAggregatingNode);
                    currentGreedyAggregatingNode = new AggregateNode();
                }
                passageList.Extend(childNode.PassageList);
            }
            else
            {
                currentAggregatingNode.AddNode(childNode);

                if (_greedilyAggregateSiblingNodes)
                {
                    if (!SECTION_BREAK_HTML_TAGS.Contains(childNode.HtmlTag ?? string.Empty)
                        && currentGreedyAggregatingNode.Fits(childNode, _maxWordsPerAggregatePassage))
                    {
                        currentGreedyAggregatingNode.AddNode(childNode);
                    }
                    else
                    {
                        passageList.AddPassageForNode(currentGreedyAggregatingNode);
                        currentGreedyAggregatingNode = childNode;
                    }
                }
                else
                {
                    passageList.AddPassageForNode(childNode);
                }
            }
        }

        if (_greedilyAggregateSiblingNodes)
        {
            passageList.AddPassageForNode(currentGreedyAggregatingNode);
        }

        if (!shouldAggregateCurrentNode || !currentNode.Fits(currentAggregatingNode, _maxWordsPerAggregatePassage))
        {
            currentNode.PassageList.AddPassageForNode(currentNode);
            currentNode.PassageList.Extend(passageList);
            return currentNode;
        }

        currentNode.AddNode(currentAggregatingNode);
        return currentNode;
    }

    /// <summary>
    /// Checks if a node should be excluded.
    /// </summary>
    private bool ShouldExcludeNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Comment)
        {
            return true;
        }

        if (_htmlTagsToExclude.Contains(node.Name))
        {
            return true;
        }

        if (node.NodeType == HtmlNodeType.Element)
        {
            var classValue = node.GetAttributeValue("class", string.Empty);
            var classes = classValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (classes.Any(c => _htmlClassesToExclude.Contains(c.ToLowerInvariant())))
            {
                return true;
            }
        }

        return false;
    }
}

