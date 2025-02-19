using HtmlAgilityPack;

namespace DocumentChunker.Extensions;

public static class HtmlNodeExtensions
{
    public static bool HasClass(this HtmlNode node)
    {
        return node.Attributes.Contains("class");
    }

    public static IEnumerable<string> GetClasses(this HtmlNode node)
    {
        return node.GetAttributeValue("class", "").Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
    }
}