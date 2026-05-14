using DriveSearch.Core.Interfaces;
using UglyToad.PdfPig;
using System.Text;

namespace DriveSearch.Infrastructure.TextExtraction;

/// <summary>
/// Extracts text from PDF files using PdfPig.
/// </summary>
public class PdfExtractor : ITextExtractor
{
    public IEnumerable<string> SupportedMimeTypes => new[] { "application/pdf" };

    public async Task<string> ExtractTextAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = PdfDocument.Open(filePath);
                var text = new StringBuilder();

                foreach (var page in document.GetPages())
                {
                    text.AppendLine(page.Text);
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting PDF text from {filePath}: {ex.Message}");
                return string.Empty;
            }
        });
    }
}
