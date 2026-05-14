using DriveSearch.Core.Interfaces;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.Llm;
using DriveSearch.Infrastructure.Search;

namespace DriveSearch.Application.Services;

/// <summary>
/// RAG (Retrieval-Augmented Generation) service.
/// Orchestrates search, snippet extraction, and answer generation.
/// </summary>
public class RagService
{
    private readonly HybridSearchService _searchService;
    private readonly SnippetExtractor _snippetExtractor;
    private readonly EmbeddingProviderFactory _embeddingProviderFactory;
    private readonly LlmProviderFactory _llmProviderFactory;

    public RagService(
        HybridSearchService searchService,
        SnippetExtractor snippetExtractor,
        EmbeddingProviderFactory embeddingProviderFactory,
        LlmProviderFactory llmProviderFactory)
    {
        _searchService = searchService;
        _snippetExtractor = snippetExtractor;
        _embeddingProviderFactory = embeddingProviderFactory;
        _llmProviderFactory = llmProviderFactory;
    }

    /// <summary>
    /// Asks a question and generates an answer based on relevant documents.
    /// </summary>
    /// <param name="query">The question to answer</param>
    /// <param name="limit">Maximum number of documents to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>RAG result with answer and sources</returns>
    public async Task<RagResult> AskAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        // Step 1: Generate embedding for the query
        var embeddingProvider = _embeddingProviderFactory.Create();
        var queryVector = await embeddingProvider.GetEmbeddingAsync(query, "RETRIEVAL_QUERY");

        // Step 2: Search for relevant documents
        var searchResults = await _searchService.SearchAsync(query, queryVector, limit);

        if (searchResults.Count == 0)
        {
            return new RagResult
            {
                Answer = "Ich konnte keine relevanten Dokumente zu Ihrer Frage finden.",
                Sources = new List<string>(),
                DocumentCount = 0
            };
        }

        // Step 3: Extract snippets from documents
        var context = _snippetExtractor.ExtractSnippetsFromDocuments(
            searchResults.Select(r => r.Document),
            query);

        if (string.IsNullOrWhiteSpace(context))
        {
            return new RagResult
            {
                Answer = "Die gefundenen Dokumente enthalten keine relevanten Informationen.",
                Sources = searchResults.Select(r => r.Document.Name).ToList(),
                DocumentCount = searchResults.Count
            };
        }

        // Step 4: Generate answer using LLM
        Console.WriteLine($"[RagService] Creating LLM provider...");
        var llmProvider = _llmProviderFactory.Create();
        Console.WriteLine($"[RagService] Using LLM provider: {llmProvider.ProviderName}");
        Console.WriteLine($"[RagService] Generating answer with {searchResults.Count} documents, context length: {context.Length}");

        var answer = await llmProvider.GenerateAnswerAsync(query, context, ct);

        Console.WriteLine($"[RagService] Answer generated successfully, length: {answer?.Length ?? 0}");

        // Step 5: Return result with sources
        return new RagResult
        {
            Answer = answer,
            Sources = searchResults.Select(r => r.Document.Name).Distinct().ToList(),
            DocumentCount = searchResults.Count,
            ProviderUsed = llmProvider.ProviderName
        };
    }
}

/// <summary>
/// Result of a RAG query.
/// </summary>
public class RagResult
{
    /// <summary>
    /// Generated answer to the question.
    /// </summary>
    public required string Answer { get; set; }

    /// <summary>
    /// List of source document names used for the answer.
    /// </summary>
    public required List<string> Sources { get; set; }

    /// <summary>
    /// Number of documents retrieved.
    /// </summary>
    public int DocumentCount { get; set; }

    /// <summary>
    /// Name of the LLM provider used (e.g., "Gemini", "Claude").
    /// </summary>
    public string? ProviderUsed { get; set; }
}
