using DriveSearch.Core.Models;

namespace DriveSearch.Infrastructure.Search;

/// <summary>
/// Combines vector (semantic) and full-text (keyword) search results.
/// Implements intelligent merge strategy prioritizing results found by both methods.
/// </summary>
public class HybridSearchService
{
    private readonly VectorSearchService _vectorSearch;
    private readonly FullTextSearchService _ftsSearch;

    /// <summary>
    /// German and English stop words to remove from queries.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // German
        "suche", "such", "finde", "finden", "zeige", "zeig", "wo", "ist", "sind",
        "mein", "meine", "meinen", "meiner", "meinem", "dein", "deine",
        "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einem",
        "ich", "du", "er", "sie", "es", "wir", "ihr",
        "nach", "von", "für", "mit", "zu", "zum", "zur", "aus", "bei", "über",
        "und", "oder", "aber", "nicht", "auch", "noch", "schon", "nur",
        "was", "wie", "wer", "wann", "welche", "welcher", "welches",
        "hat", "haben", "bin", "habe", "gibt", "gibt's",
        "alle", "alles", "allen",
        // English
        "the", "a", "an", "is", "are", "my", "your", "find", "search", "show",
        "me", "for", "from", "with", "in", "on", "of", "to"
    };

    public HybridSearchService(VectorSearchService vectorSearch, FullTextSearchService ftsSearch)
    {
        _vectorSearch = vectorSearch;
        _ftsSearch = ftsSearch;
    }

    /// <summary>
    /// Performs hybrid search combining vector and FTS results.
    /// Query is cleaned (stopwords removed) before searching.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="queryVector">Query embedding vector (for semantic search)</param>
    /// <param name="limit">Maximum results to return</param>
    /// <returns>Merged and deduplicated search results</returns>
    public async Task<List<SearchResult>> SearchAsync(string query, float[] queryVector, int limit = 5)
    {
        // Clean query (remove stopwords)
        var cleanedQuery = CleanQuery(query);

        // Execute both searches in parallel
        var vectorTask = _vectorSearch.SearchAsync(queryVector, limit);
        var ftsTask = _ftsSearch.SearchAsync(cleanedQuery, limit);

        await Task.WhenAll(vectorTask, ftsTask);

        var vectorResults = await vectorTask;
        var ftsResults = await ftsTask;

        // Merge results with priority strategy
        return MergeResults(vectorResults, ftsResults, limit);
    }

    /// <summary>
    /// Cleans query by removing stopwords.
    /// Only keeps relevant search terms.
    /// </summary>
    public static string CleanQuery(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !StopWords.Contains(w))
            .ToList();

        return words.Count > 0 ? string.Join(" ", words) : query;
    }

    /// <summary>
    /// Merges vector and FTS results with intelligent priority strategy:
    /// 1. Results in BOTH lists (highest relevance)
    /// 2. FTS-only results (exact text matches)
    /// 3. Vector-only results (only if FTS found nothing)
    /// </summary>
    private List<SearchResult> MergeResults(
        List<SearchResult> vectorResults,
        List<SearchResult> ftsResults,
        int limit)
    {
        var merged = new List<SearchResult>();
        var seen = new HashSet<string>(); // Track FileIds to avoid duplicates

        // Get FTS file IDs for quick lookup
        var ftsFileIds = ftsResults.Select(r => r.Document.FileId).ToHashSet();

        // Priority 1: Results in BOTH searches (highest confidence)
        foreach (var result in vectorResults)
        {
            if (ftsFileIds.Contains(result.Document.FileId))
            {
                result.Source = "both"; // Mark as found by both methods
                merged.Add(result);
                seen.Add(result.Document.FileId);
            }
        }

        // Priority 2: FTS-only results (exact text matches)
        foreach (var result in ftsResults)
        {
            if (!seen.Contains(result.Document.FileId))
            {
                merged.Add(result);
                seen.Add(result.Document.FileId);
            }
        }

        // Priority 3: Vector-only results (semantic matches)
        // Only add if FTS found nothing at all
        if (ftsResults.Count == 0)
        {
            foreach (var result in vectorResults)
            {
                if (!seen.Contains(result.Document.FileId))
                {
                    merged.Add(result);
                    seen.Add(result.Document.FileId);
                }
            }
        }

        // Return up to the requested limit
        return merged.Take(limit).ToList();
    }
}
