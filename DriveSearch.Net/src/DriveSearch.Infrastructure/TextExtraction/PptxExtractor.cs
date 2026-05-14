using DriveSearch.Core.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System.Text;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace DriveSearch.Infrastructure.TextExtraction;

/// <summary>
/// Extracts text from PPTX files using OpenXML SDK.
/// </summary>
public class PptxExtractor : ITextExtractor
{
    public IEnumerable<string> SupportedMimeTypes => new[]
    {
        "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public async Task<string> ExtractTextAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = PresentationDocument.Open(filePath, false);
                var presentationPart = document.PresentationPart;

                if (presentationPart == null)
                    return string.Empty;

                var text = new StringBuilder();

                // Iterate through all slides
                foreach (var slidePart in presentationPart.SlideParts)
                {
                    var slide = slidePart.Slide;

                    // Extract text from all shapes in the slide
                    foreach (var shape in slide.Descendants<Shape>())
                    {
                        foreach (var paragraph in shape.Descendants<Drawing.Paragraph>())
                        {
                            foreach (var textRun in paragraph.Descendants<Drawing.Run>())
                            {
                                var textElement = textRun.GetFirstChild<Drawing.Text>();
                                if (textElement != null)
                                {
                                    text.Append(textElement.Text);
                                    text.Append(' ');
                                }
                            }
                            text.AppendLine();
                        }
                    }
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting PPTX text from {filePath}: {ex.Message}");
                return string.Empty;
            }
        });
    }
}
