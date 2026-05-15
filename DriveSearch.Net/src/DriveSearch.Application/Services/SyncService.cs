using DriveSearch.Core.Interfaces;
using DriveSearch.Core.Models;
using DriveSearch.Infrastructure.Drive;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.TextExtraction;

namespace DriveSearch.Application.Services;

/// <summary>
/// Orchestrates synchronization between Google Drive and local database.
/// Implements delta sync strategy (only processes new/modified files).
/// </summary>
public class SyncService
{
    private readonly GoogleDriveClient _driveClient;
    private readonly IDocumentRepository _documentRepository;
    private readonly TextExtractionFactory _textExtractor;
    private readonly IEmbeddingProvider _embeddingProvider;

    public SyncService(
        GoogleDriveClient driveClient,
        IDocumentRepository documentRepository,
        TextExtractionFactory textExtractor,
        EmbeddingProviderFactory embeddingProviderFactory)
    {
        _driveClient = driveClient;
        _documentRepository = documentRepository;
        _textExtractor = textExtractor;
        _embeddingProvider = embeddingProviderFactory.Create();
    }

    /// <summary>
    /// Performs full synchronization:
    /// 1. Lists all files from Google Drive
    /// 2. Detects deletions (files in DB but not in Drive)
    /// 3. Processes new/modified files (download, extract, embed, store)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync statistics</returns>
    public async Task<SyncResult> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var stats = new SyncResult { StartTime = DateTime.UtcNow };

        try
        {
            // Step 1: List all files and folders from Google Drive
            Console.WriteLine("Listing files from Google Drive...");
            var driveFiles = await _driveClient.ListAllFilesAsync(cancellationToken);
            Console.WriteLine($"Found {driveFiles.Count} files in Drive");

            var folderNames = await _driveClient.GetFolderNamesAsync(cancellationToken);

            // Step 2: Get existing file IDs from database
            var dbFileIds = (await _documentRepository.GetAllFileIdsAsync()).ToHashSet();

            // Step 3: Detect deletions (in DB but not in Drive)
            var driveFileIds = driveFiles.Select(f => f.Id).ToHashSet();
            var deletedIds = dbFileIds.Except(driveFileIds).ToList();

            Console.WriteLine($"Deleting {deletedIds.Count} removed files...");
            foreach (var fileId in deletedIds)
            {
                await _documentRepository.DeleteByFileIdAsync(fileId);
                stats.FilesDeleted++;
            }

            // Step 4: Process new/modified files
            Console.WriteLine($"Processing {driveFiles.Count} files...");
            foreach (var file in driveFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var result = await ProcessFileAsync(file, folderNames, cancellationToken);
                    if (result == FileProcessResult.Added)
                        stats.FilesAdded++;
                    else if (result == FileProcessResult.Updated)
                        stats.FilesUpdated++;
                    else if (result == FileProcessResult.Skipped)
                        stats.FilesSkipped++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {file.Name}: {ex.Message}");
                    stats.FilesErrored++;
                }
            }

            stats.EndTime = DateTime.UtcNow;
            stats.Success = true;
            await _documentRepository.UpdateLastSyncAtAsync(stats.EndTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sync failed: {ex.Message}");
            stats.EndTime = DateTime.UtcNow;
            stats.Success = false;
            stats.ErrorMessage = ex.Message;
        }

        return stats;
    }

    /// <summary>
    /// Processes a single file: checks if update needed, downloads, extracts, embeds, stores.
    /// </summary>
    private async Task<FileProcessResult> ProcessFileAsync(
        DriveFile file,
        Dictionary<string, string> folderNames,
        CancellationToken cancellationToken)
    {
        // Check if file needs updating (compare modifiedTime)
        var existing = await _documentRepository.GetByFileIdAsync(file.Id);

        if (existing != null && existing.ModifiedAt == file.ModifiedTime)
        {
            // Backfill FolderPath if it was added after initial sync
            if (existing.FolderPath == null && file.ParentId != null && folderNames.TryGetValue(file.ParentId, out var fn))
                await _documentRepository.UpdateFolderPathAsync(file.Id, fn);

            // Backfill chunks for documents indexed before chunking was introduced
            if (!string.IsNullOrWhiteSpace(existing.Text) && !await _documentRepository.HasChunksAsync(existing.Id))
            {
                Console.WriteLine($"Backfilling chunks for: {existing.Name}");
                await _documentRepository.DeleteChunksAsync(existing.Id);
                var backfillChunks = TextChunker.Split(existing.Text);
                foreach (var (chunk, idx) in backfillChunks.Select((c, i) => (c, i)))
                {
                    var chunkId = await _documentRepository.SaveChunkAsync(existing.Id, idx, chunk);
                    var emb = await _embeddingProvider.GetEmbeddingAsync(chunk);
                    await _documentRepository.SaveChunkEmbeddingAsync(existing.Id, chunkId, emb);
                }
            }

            return FileProcessResult.Skipped;
        }

        var isNewFile = existing == null;

        // Download file to temporary location
        var tempPath = Path.GetTempFileName();
        try
        {
            Console.WriteLine($"Downloading: {file.Name}");
            await _driveClient.DownloadFileAsync(file.Id, file.MimeType, tempPath, cancellationToken);

            // Extract text
            Console.WriteLine($"Extracting text from: {file.Name}");
            var text = await _textExtractor.ExtractTextAsync(tempPath, file.MimeType);

            // Create/update document
            var folderPath = file.ParentId != null && folderNames.TryGetValue(file.ParentId, out var fn) ? fn : null;

            var document = new Document
            {
                FileId = file.Id,
                Name = file.Name,
                MimeType = file.MimeType,
                Size = file.Size,
                CreatedAt = file.CreatedTime,
                ModifiedAt = file.ModifiedTime,
                Text = text,
                IndexedAt = DateTime.UtcNow,
                FolderPath = folderPath
            };

            var documentId = await _documentRepository.UpsertAsync(document);

            // Chunk and embed
            await _documentRepository.DeleteChunksAsync(documentId);

            if (string.IsNullOrWhiteSpace(text))
            {
                // No extractable text — embed filename as a single document-level fallback
                Console.WriteLine($"Generating filename embedding for: {file.Name}");
                var embedding = await _embeddingProvider.GetEmbeddingAsync(file.Name);
                await _documentRepository.SaveEmbeddingAsync(documentId, embedding);
            }
            else
            {
                var chunks = TextChunker.Split(text);
                Console.WriteLine($"Generating {chunks.Count} chunk embeddings for: {file.Name}");
                foreach (var (chunk, idx) in chunks.Select((c, i) => (c, i)))
                {
                    var chunkId = await _documentRepository.SaveChunkAsync(documentId, idx, chunk);
                    var embedding = await _embeddingProvider.GetEmbeddingAsync(chunk);
                    await _documentRepository.SaveChunkEmbeddingAsync(documentId, chunkId, embedding);
                }
            }

            Console.WriteLine($"✓ Processed: {file.Name}");

            return isNewFile ? FileProcessResult.Added : FileProcessResult.Updated;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

/// <summary>
/// Result of file processing operation.
/// </summary>
public enum FileProcessResult
{
    Added,
    Updated,
    Skipped,
    Errored
}

/// <summary>
/// Statistics from a synchronization run.
/// </summary>
public class SyncResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public int FilesAdded { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesErrored { get; set; }

    public TimeSpan Duration => EndTime - StartTime;

    public override string ToString()
    {
        return $"Sync completed in {Duration.TotalSeconds:F1}s: " +
               $"+{FilesAdded} ~{FilesUpdated} -{FilesDeleted} ={FilesSkipped} !{FilesErrored}";
    }
}
