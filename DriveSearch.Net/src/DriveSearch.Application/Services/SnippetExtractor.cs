using DriveSearch.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace DriveSearch.Application.Services;

/// <summary>
/// Extracts relevant text snippets from documents for RAG context.
/// Finds passages containing query terms with surrounding context.
/// </summary>
public class SnippetExtractor
{
    private const int SnippetContextWords = 50; // Words before and after match
    private const int MaxSnippetsPerDocument = 3;
    private const int MaxSnippetLength = 500; // Characters

    /// <summary>
    /// Extracts relevant snippets from a document based on query terms.
    /// </summary>
    public string ExtractSnippets(Document document, string query)
    {
        if (string.IsNullOrWhiteSpace(document.Text))
        {
            return string.Empty;
        }

        var queryTerms = ExtractQueryTerms(query);
        if (queryTerms.Count == 0)
        {
            return TruncateText(document.Text, MaxSnippetLength);
        }

        var snippets = FindRelevantSnippets(document.Text, queryTerms);

        return FormatSnippets(document.Name, snippets);
    }

    /// <summary>
    /// Extracts snippets from multiple documents.
    /// </summary>
    public string ExtractSnippetsFromDocuments(IEnumerable<Document> documents, string query)
    {
        var sb = new StringBuilder();

        foreach (var document in documents)
        {
            var snippet = ExtractSnippets(document, query);
            if (!string.IsNullOrWhiteSpace(snippet))
            {
                sb.AppendLine(snippet);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private List<string> ExtractQueryTerms(string query)
    {
        // Extract significant words (3+ characters, not stopwords)
        var words = Regex.Matches(query.ToLowerInvariant(), @"\b\w{3,}\b")
            .Select(m => m.Value)
            .Where(w => !IsStopword(w))
            .Distinct()
            .ToList();

        return words;
    }

    private List<string> FindRelevantSnippets(string text, List<string> queryTerms)
    {
        var snippets = new List<string>();
        var sentences = SplitIntoSentences(text);

        foreach (var sentence in sentences)
        {
            var lowerSentence = sentence.ToLowerInvariant();
            var matchCount = queryTerms.Count(term => lowerSentence.Contains(term));

            if (matchCount > 0)
            {
                var snippet = ExtractContext(sentence, MaxSnippetLength);
                snippets.Add(snippet);

                if (snippets.Count >= MaxSnippetsPerDocument)
                {
                    break;
                }
            }
        }

        // If no matches, return first snippet
        if (snippets.Count == 0 && sentences.Count > 0)
        {
            snippets.Add(ExtractContext(sentences[0], MaxSnippetLength));
        }

        return snippets;
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Split on sentence boundaries (., !, ?, newlines)
        var sentences = Regex.Split(text, @"(?<=[.!?\n])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    private string ExtractContext(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        // Truncate and add ellipsis
        var truncated = text.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > 0)
        {
            truncated = truncated.Substring(0, lastSpace);
        }

        return truncated + "...";
    }

    private string FormatSnippets(string documentName, List<string> snippets)
    {
        if (snippets.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📄 {documentName}");

        foreach (var snippet in snippets)
        {
            sb.AppendLine($"  • {snippet}");
        }

        return sb.ToString().TrimEnd();
    }

    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var truncated = text.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > 0)
        {
            truncated = truncated.Substring(0, lastSpace);
        }

        return truncated + "...";
    }

    private bool IsStopword(string word)
    {
        // Common German and English stopwords (subset for performance)
        var stopwords = new HashSet<string>
        {
            // German
            "der", "die", "das", "und", "ist", "ein", "eine", "mit", "für", "auf", "von", "dem", "den",
            "des", "als", "auch", "oder", "wie", "bei", "aus", "nach", "über", "bis", "wird", "sich",
            "werden", "kann", "hat", "sind", "zum", "zur", "eine", "einer", "einem", "einen",
            // English
            "the", "and", "that", "have", "with", "this", "from", "they", "will", "would", "there",
            "their", "what", "about", "which", "when", "make", "like", "time", "just", "know",
            "take", "into", "year", "your", "some", "could", "them", "than", "then", "now"
        };

        return stopwords.Contains(word);
    }
}
