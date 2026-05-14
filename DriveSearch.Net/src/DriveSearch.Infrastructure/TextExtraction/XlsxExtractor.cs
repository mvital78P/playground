using DriveSearch.Core.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace DriveSearch.Infrastructure.TextExtraction;

/// <summary>
/// Extracts text from XLSX files using OpenXML SDK.
/// </summary>
public class XlsxExtractor : ITextExtractor
{
    public IEnumerable<string> SupportedMimeTypes => new[]
    {
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public async Task<string> ExtractTextAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = SpreadsheetDocument.Open(filePath, false);
                var workbookPart = document.WorkbookPart;

                if (workbookPart == null)
                    return string.Empty;

                var text = new StringBuilder();
                var stringTable = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault()?.SharedStringTable;

                foreach (var worksheetPart in workbookPart.WorksheetParts)
                {
                    var worksheet = worksheetPart.Worksheet;
                    var sheetData = worksheet.GetFirstChild<SheetData>();

                    if (sheetData == null)
                        continue;

                    foreach (var row in sheetData.Elements<Row>())
                    {
                        foreach (var cell in row.Elements<Cell>())
                        {
                            var cellValue = GetCellValue(cell, stringTable);
                            if (!string.IsNullOrWhiteSpace(cellValue))
                            {
                                text.Append(cellValue);
                                text.Append(' ');
                            }
                        }
                        text.AppendLine();
                    }
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting XLSX text from {filePath}: {ex.Message}");
                return string.Empty;
            }
        });
    }

    private static string? GetCellValue(Cell cell, SharedStringTable? stringTable)
    {
        if (cell.CellValue == null)
            return null;

        var value = cell.CellValue.Text;

        // If it's a shared string, look up the actual value
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString && stringTable != null)
        {
            if (int.TryParse(value, out int index))
            {
                return stringTable.ElementAt(index).InnerText;
            }
        }

        return value;
    }
}
