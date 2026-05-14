using DriveSearch.Core.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using System.Text;

namespace DriveSearch.Infrastructure.TextExtraction;

/// <summary>
/// Extracts text from DOCX files using OpenXML SDK.
/// </summary>
public class DocxExtractor : ITextExtractor
{
    public IEnumerable<string> SupportedMimeTypes => new[]
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public async Task<string> ExtractTextAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                var body = document.MainDocumentPart?.Document.Body;

                if (body == null)
                    return string.Empty;

                return body.InnerText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting DOCX text from {filePath}: {ex.Message}");
                return string.Empty;
            }
        });
    }
}
