using System.ComponentModel;
using System.Text.Json;
using DriveSearch.Application.Services;
using DriveSearch.Core.Interfaces;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.Search;
using ModelContextProtocol.Server;

namespace DriveSearch.McpServer;

[McpServerToolType]
public class DriveSearchTools(
    IDocumentRepository repository,
    HybridSearchService hybridSearch,
    EmbeddingProviderFactory embeddingFactory,
    RagService ragService)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "search_docs"), Description("Google Drive Dokumente semantisch und per Volltext durchsuchen. Gibt die relevantesten Dokumente mit Vorschau zurück.")]
    public async Task<string> SearchDocs(
        [Description("Suchanfrage auf Deutsch oder Englisch")] string query,
        [Description("Maximale Anzahl Ergebnisse (Standard: 5)")] int limit = 5)
    {
        var embeddingProvider = embeddingFactory.Create();
        var queryVector = await embeddingProvider.GetEmbeddingAsync(query, "RETRIEVAL_QUERY");
        var results = await hybridSearch.SearchAsync(query, queryVector, limit);

        var response = new
        {
            query,
            count = results.Count,
            results = results.Select(r => new
            {
                id = r.Document.Id,
                name = r.Document.Name,
                mime_type = r.Document.MimeType,
                modified_at = r.Document.ModifiedAt?.ToString("O"),
                preview = r.Preview,
                source = r.Source
            })
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    [McpServerTool(Name = "get_doc"), Description("Vollständigen Inhalt eines Dokuments anhand seiner ID abrufen. Die ID kommt aus den Suchergebnissen von search_docs.")]
    public async Task<string> GetDoc(
        [Description("Die numerische ID des Dokuments")] int document_id)
    {
        var doc = await repository.GetByIdAsync(document_id);
        if (doc is null)
            return JsonSerializer.Serialize(new { error = $"Dokument mit ID {document_id} nicht gefunden." });

        var response = new
        {
            id = doc.Id,
            file_id = doc.FileId,
            name = doc.Name,
            mime_type = doc.MimeType,
            size = doc.Size,
            created_at = doc.CreatedAt?.ToString("O"),
            modified_at = doc.ModifiedAt?.ToString("O"),
            indexed_at = doc.IndexedAt?.ToString("O"),
            text = doc.Text ?? "",
            has_text = !string.IsNullOrWhiteSpace(doc.Text)
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    [McpServerTool(Name = "list_docs"), Description("Alle indexierten Google Drive Dokumente auflisten.")]
    public async Task<string> ListDocs(
        [Description("Anzahl Dokumente pro Seite (Standard: 20)")] int limit = 20,
        [Description("Seiten-Offset für Paginierung")] int offset = 0,
        [Description("Optionaler MIME-Type Filter, z.B. 'pdf', 'word', 'spreadsheet'")] string mime_filter = "")
    {
        var (total, documents) = await repository.ListDocumentsAsync(limit, offset, mime_filter);

        var response = new
        {
            total,
            limit,
            offset,
            documents = documents.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                mime_type = d.MimeType,
                size = d.Size,
                modified_at = d.ModifiedAt?.ToString("O"),
                indexed_at = d.IndexedAt?.ToString("O")
            })
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    [McpServerTool(Name = "ask"), Description("Frage an die Dokumente stellen (RAG). Sucht relevante Dokumente und generiert eine Antwort.")]
    public async Task<string> Ask(
        [Description("Die Frage an die Dokumente.")] string query,
        [Description("Maximale Anzahl Ergebnisse für die Suche (Standard: 5).")] int limit = 5)
    {
        var result = await ragService.AskAsync(query, limit);

        var response = new
        {
            query,
            answer = result.Answer,
            sources = result.Sources
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    [McpServerTool(Name = "drive_status"), Description("Statistik der indizierten Dokumente anzeigen (Anzahl, letzter Sync).")]
    public async Task<string> DriveStatus()
    {
        var (total, lastSync) = await repository.GetStatsAsync();

        var response = new
        {
            total_documents = total,
            last_sync = lastSync?.ToString("O")
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }
}
