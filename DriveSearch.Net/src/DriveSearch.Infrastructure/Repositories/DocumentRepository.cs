using DriveSearch.Core.Interfaces;
using DriveSearch.Core.Models;
using DriveSearch.Infrastructure.Data;
using DriveSearch.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DriveSearch.Infrastructure.Repositories;

/// <summary>
/// Repository for document database operations.
/// Handles CRUD operations, embeddings, and statistics.
/// </summary>
public class DocumentRepository : IDocumentRepository
{
    private readonly DriveSearchContext _context;

    public DocumentRepository(DriveSearchContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetAllFileIdsAsync()
    {
        return await _context.Documents
            .Select(d => d.FileId)
            .ToListAsync();
    }

    public async Task<Document?> GetByFileIdAsync(string fileId)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.FileId == fileId);
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        return await _context.Documents
            .Include(d => d.Embedding)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<int> UpsertAsync(Document document)
    {
        var existing = await GetByFileIdAsync(document.FileId);

        if (existing != null)
        {
            // Update existing document
            existing.Name = document.Name;
            existing.MimeType = document.MimeType;
            existing.Size = document.Size;
            existing.CreatedAt = document.CreatedAt;
            existing.ModifiedAt = document.ModifiedAt;
            existing.Text = document.Text;
            existing.IndexedAt = document.IndexedAt ?? DateTime.UtcNow;

            _context.Documents.Update(existing);
            await _context.SaveChangesAsync();

            return existing.Id;
        }
        else
        {
            // Insert new document
            document.IndexedAt = document.IndexedAt ?? DateTime.UtcNow;
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            return document.Id;
        }
    }

    public async Task DeleteByFileIdAsync(string fileId)
    {
        var document = await GetByFileIdAsync(fileId);
        if (document != null)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SaveEmbeddingAsync(int documentId, float[] vector)
    {
        // Serialize vector to binary
        var vectorBlob = VectorSearchService.SerializeVector(vector);

        var existing = await _context.Embeddings
            .FirstOrDefaultAsync(e => e.DocumentId == documentId);

        if (existing != null)
        {
            // Update existing embedding
            existing.Vector = vectorBlob;
            _context.Embeddings.Update(existing);
        }
        else
        {
            // Insert new embedding
            var embedding = new Embedding
            {
                DocumentId = documentId,
                Vector = vectorBlob
            };
            _context.Embeddings.Add(embedding);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<(int totalDocuments, DateTime? lastSync)> GetStatsAsync()
    {
        var totalDocuments = await _context.Documents.CountAsync();

        var lastSync = await _context.Documents
            .OrderByDescending(d => d.IndexedAt)
            .Select(d => d.IndexedAt)
            .FirstOrDefaultAsync();

        return (totalDocuments, lastSync);
    }

    public async Task<List<Document>> GetRecentDocumentsAsync(int limit = 10)
    {
        return await _context.Documents
            .OrderByDescending(d => d.IndexedAt)
            .Take(limit)
            .ToListAsync();
    }
}
