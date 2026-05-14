namespace DriveSearch.Core.Interfaces;

/// <summary>
/// Interface for Large Language Model providers.
/// Used for generating answers in RAG (Retrieval-Augmented Generation) scenarios.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generates an answer based on the query and provided context.
    /// </summary>
    /// <param name="query">User's question</param>
    /// <param name="context">Retrieved document context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated answer</returns>
    Task<string> GenerateAnswerAsync(
        string query,
        string context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provider name (e.g., "gemini", "claude", "ollama", "lmstudio")
    /// </summary>
    string ProviderName { get; }
}
