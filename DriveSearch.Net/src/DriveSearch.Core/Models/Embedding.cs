namespace DriveSearch.Core.Models;

/// <summary>
/// Represents a vector embedding for semantic search.
/// Stores the embedding as a binary blob (float32 array).
/// </summary>
public class Embedding
{
    /// <summary>
    /// Primary key (database ID)
    /// </summary>
    public int Id { get; set; }

    public int DocumentId { get; set; }

    /// <summary>
    /// If set, this embedding belongs to a specific chunk rather than the whole document.
    /// </summary>
    public int? ChunkId { get; set; }

    public required byte[] Vector { get; set; }

    public Document? Document { get; set; }
    public DocumentChunk? Chunk { get; set; }
}
