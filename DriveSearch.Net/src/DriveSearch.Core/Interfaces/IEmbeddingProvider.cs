namespace DriveSearch.Core.Interfaces;

/// <summary>
/// Interface for generating text embeddings (vector representations).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="taskType">Task type hint (e.g., "RETRIEVAL_DOCUMENT", "RETRIEVAL_QUERY")</param>
    /// <returns>Embedding vector as float array</returns>
    Task<float[]> GetEmbeddingAsync(string text, string taskType = "RETRIEVAL_DOCUMENT");

    /// <summary>
    /// Dimension of the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }
}
