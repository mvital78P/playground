using DriveSearch.Core.Interfaces;

namespace DriveSearch.Infrastructure.TextExtraction;

/// <summary>
/// Factory for creating appropriate text extractors based on MIME type.
/// </summary>
public class TextExtractionFactory
{
    private readonly Dictionary<string, ITextExtractor> _extractors;

    public TextExtractionFactory()
    {
        // Register all extractors
        var extractorInstances = new ITextExtractor[]
        {
            new PdfExtractor(),
            new DocxExtractor(),
            new XlsxExtractor(),
            new PptxExtractor()
        };

        // Build MIME type -> extractor mapping
        _extractors = new Dictionary<string, ITextExtractor>(StringComparer.OrdinalIgnoreCase);

        foreach (var extractor in extractorInstances)
        {
            foreach (var mimeType in extractor.SupportedMimeTypes)
            {
                _extractors[mimeType] = extractor;
            }
        }
    }

    /// <summary>
    /// Extracts text from a file based on its MIME type.
    /// Returns empty string if MIME type is not supported or extraction fails.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="mimeType">MIME type of the file</param>
    /// <returns>Extracted text content</returns>
    public async Task<string> ExtractTextAsync(string filePath, string mimeType)
    {
        // Google Workspace files are exported as PDF
        if (mimeType.StartsWith("application/vnd.google-apps."))
        {
            mimeType = "application/pdf";
        }

        if (!_extractors.TryGetValue(mimeType, out var extractor))
        {
            Console.WriteLine($"No extractor found for MIME type: {mimeType}");
            return string.Empty;
        }

        try
        {
            return await extractor.ExtractTextAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Text extraction failed for {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks if a MIME type is supported for text extraction.
    /// </summary>
    public bool IsSupported(string mimeType)
    {
        if (mimeType.StartsWith("application/vnd.google-apps."))
            return true; // Google Workspace files are exported as PDF

        return _extractors.ContainsKey(mimeType);
    }

    /// <summary>
    /// Gets all supported MIME types.
    /// </summary>
    public IEnumerable<string> GetSupportedMimeTypes()
    {
        return _extractors.Keys;
    }
}
