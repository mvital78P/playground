using DriveSearch.Application.Services;
using DriveSearch.Core.Models;
using DriveSearch.Infrastructure.Search;
using System.Text;

namespace DriveSearch.TelegramBot.Formatters;

/// <summary>
/// Formats responses for Telegram with HTML markup.
/// </summary>
public static class ResponseFormatter
{
    /// <summary>
    /// Formats search results for display in Telegram.
    /// </summary>
    public static string FormatSearchResults(List<SearchResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🔍 <b>Suchergebnisse ({results.Count})</b>");
        sb.AppendLine();

        for (int i = 0; i < Math.Min(10, results.Count); i++)
        {
            var result = results[i];
            var doc = result.Document;
            var driveUrl = $"https://drive.google.com/open?id={doc.FileId}";

            sb.AppendLine($"{i + 1}. 📄 <a href=\"{driveUrl}\"><b>{EscapeHtml(doc.Name)}</b></a>");

            if (!string.IsNullOrWhiteSpace(doc.Text))
            {
                var preview = doc.Text.Length > 150
                    ? doc.Text.Substring(0, 150) + "..."
                    : doc.Text;
                sb.AppendLine($"   <i>{EscapeHtml(preview)}</i>");
            }

            if (doc.ModifiedAt.HasValue)
            {
                sb.AppendLine($"   📅 {doc.ModifiedAt.Value:dd.MM.yyyy}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats RAG result for display in Telegram.
    /// </summary>
    public static string FormatRagResult(RagResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("💬 <b>Antwort:</b>");
        sb.AppendLine();
        sb.AppendLine(EscapeHtml(result.Answer));
        sb.AppendLine();

        if (result.SourceDocuments.Count > 0)
        {
            sb.AppendLine("📚 <b>Quellen:</b>");
            foreach (var doc in result.SourceDocuments.Take(5))
            {
                var driveUrl = $"https://drive.google.com/open?id={doc.FileId}";
                sb.AppendLine($"  • <a href=\"{driveUrl}\">{EscapeHtml(doc.Name)}</a>");
            }
            sb.AppendLine();
        }
        else if (result.Sources.Count > 0)
        {
            sb.AppendLine("📚 <b>Quellen:</b>");
            foreach (var source in result.Sources.Take(5))
            {
                sb.AppendLine($"  • {EscapeHtml(source)}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(result.ProviderUsed))
        {
            sb.AppendLine($"🤖 <i>Generiert mit: {result.ProviderUsed}</i>");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats recent documents for display in Telegram.
    /// </summary>
    public static string FormatRecentDocuments(List<Document> documents)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📄 <b>Zuletzt indexierte Dokumente ({documents.Count})</b>");
        sb.AppendLine();

        for (int i = 0; i < Math.Min(10, documents.Count); i++)
        {
            var doc = documents[i];
            var driveUrl = $"https://drive.google.com/open?id={doc.FileId}";

            sb.AppendLine($"{i + 1}. <a href=\"{driveUrl}\"><b>{EscapeHtml(doc.Name)}</b></a>");

            if (doc.IndexedAt.HasValue)
            {
                sb.AppendLine($"   🕐 {doc.IndexedAt.Value:dd.MM.yyyy HH:mm}");
            }

            if (doc.ModifiedAt.HasValue)
            {
                sb.AppendLine($"   📅 Geändert: {doc.ModifiedAt.Value:dd.MM.yyyy}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Escapes HTML special characters for Telegram.
    /// </summary>
    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
