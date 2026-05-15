namespace DriveSearch.Core.Models;

/// <summary>
/// Represents a search result with relevance scoring.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// The document that matched the search
    /// </summary>
    public required Document Document { get; set; }

    /// <summary>
    /// Relevance score/distance (lower is better for cosine distance)
    /// For vector search: cosine distance (0.0 = identical, 2.0 = opposite)
    /// For FTS: rank score
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Source of the search result ("vector", "fts", or "both")
    /// </summary>
    public string Source { get; set; } = "unknown";

    /// <summary>
    /// Short preview for display (first 200 chars of the matched text).
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// Full text of the matched chunk, used as RAG context.
    /// Null for FTS results or pre-chunking document embeddings.
    /// </summary>
    public string? ChunkText { get; set; }
}
