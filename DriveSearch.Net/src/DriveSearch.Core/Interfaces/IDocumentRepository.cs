using DriveSearch.Core.Models;

namespace DriveSearch.Core.Interfaces;

/// <summary>
/// Repository interface for document data access operations.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Gets all file IDs from the database.
    /// </summary>
    Task<List<string>> GetAllFileIdsAsync();

    /// <summary>
    /// Gets a document by its Google Drive file ID.
    /// </summary>
    Task<Document?> GetByFileIdAsync(string fileId);

    /// <summary>
    /// Gets a document by its database ID.
    /// </summary>
    Task<Document?> GetByIdAsync(int id);

    /// <summary>
    /// Upserts a document (insert or update based on FileId).
    /// Returns the database ID of the document.
    /// </summary>
    Task<int> UpsertAsync(Document document);

    /// <summary>
    /// Deletes a document by its Google Drive file ID.
    /// </summary>
    Task DeleteByFileIdAsync(string fileId);

    /// <summary>
    /// Saves an embedding vector for a document.
    /// </summary>
    Task SaveEmbeddingAsync(int documentId, float[] vector);

    /// <summary>
    /// Gets database statistics (total documents, last sync time).
    /// </summary>
    Task<(int totalDocuments, DateTime? lastSync)> GetStatsAsync();

    /// <summary>
    /// Gets the most recently indexed documents.
    /// </summary>
    Task<List<Document>> GetRecentDocumentsAsync(int limit = 10);

    /// <summary>
    /// Lists documents with pagination and optional MIME type filter.
    /// </summary>
    Task<(int total, List<Document> documents)> ListDocumentsAsync(int limit, int offset, string? mimeFilter);

    /// <summary>
    /// Updates only the FolderPath field for a document by its Google Drive file ID.
    /// </summary>
    Task UpdateFolderPathAsync(string fileId, string folderPath);

    /// <summary>
    /// Persists the timestamp of the most recent completed sync run.
    /// </summary>
    Task UpdateLastSyncAtAsync(DateTime syncTime);
}
