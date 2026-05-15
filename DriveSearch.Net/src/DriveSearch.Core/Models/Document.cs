namespace DriveSearch.Core.Models;

/// <summary>
/// Represents a document from Google Drive with extracted text content.
/// </summary>
public class Document
{
    /// <summary>
    /// Primary key (database ID)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Google Drive file ID (unique identifier from Drive API)
    /// </summary>
    public required string FileId { get; set; }

    /// <summary>
    /// Document name (filename from Google Drive)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// MIME type (e.g., application/pdf, application/vnd.openxmlformats-officedocument.wordprocessingml.document)
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Creation timestamp from Google Drive (ISO 8601 format)
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Last modification timestamp from Google Drive (ISO 8601 format)
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Extracted text content from the document
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Timestamp when this document was indexed/processed
    /// </summary>
    public DateTime? IndexedAt { get; set; }

    /// <summary>
    /// Name of the Google Drive folder this document lives in.
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>
    /// Navigation property to associated embedding vector
    /// </summary>
    public Embedding? Embedding { get; set; }
}
