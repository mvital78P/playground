namespace DriveSearch.Core.Interfaces;

/// <summary>
/// Interface for extracting text content from documents.
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text from a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(string filePath);

    /// <summary>
    /// MIME types this extractor can handle.
    /// </summary>
    IEnumerable<string> SupportedMimeTypes { get; }
}
