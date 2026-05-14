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

    /// <summary>
    /// Foreign key to the associated document
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Binary blob containing the embedding vector (serialized float32 array)
    /// Dimensions: 3072 for Gemini, 768 for Ollama/LM Studio
    /// </summary>
    public required byte[] Vector { get; set; }

    /// <summary>
    /// Navigation property to the associated document
    /// </summary>
    public Document? Document { get; set; }
}
