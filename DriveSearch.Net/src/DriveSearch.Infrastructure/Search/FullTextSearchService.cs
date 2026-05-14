using DriveSearch.Core.Models;
using DriveSearch.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DriveSearch.Infrastructure.Search;

/// <summary>
/// Service for full-text search using SQLite FTS5.
/// Performs keyword-based search with ranking.
/// </summary>
public class FullTextSearchService
{
    private readonly DriveSearchContext _context;

    public FullTextSearchService(DriveSearchContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Performs full-text search using FTS5 with AND/OR query strategies.
    /// First tries AND query (all terms must match), falls back to OR if no results.
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of search results ordered by FTS rank</returns>
    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();

        // Split query into words
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        if (words.Count == 0)
            return new List<SearchResult>();

        // Try AND query first (all terms must match)
        var results = await ExecuteFtsQueryAsync(words, useAndOperator: true, limit);

        // Fallback: OR query if AND returned no results
        if (results.Count == 0 && words.Count > 1)
        {
            results = await ExecuteFtsQueryAsync(words, useAndOperator: false, limit);
        }

        return results;
    }

    /// <summary>
    /// Executes FTS5 query with specified operator (AND or OR).
    /// </summary>
    private async Task<List<SearchResult>> ExecuteFtsQueryAsync(
        List<string> words,
        bool useAndOperator,
        int limit)
    {
        var dbConnection = _context.Database.GetDbConnection() as SqliteConnection;
        if (dbConnection == null)
            throw new InvalidOperationException("Expected SqliteConnection");

        // Ensure connection is open
        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();

        // Build FTS query: "term1" AND "term2" or "term1" OR "term2"
        var operatorStr = useAndOperator ? " AND " : " OR ";
        var ftsQuery = string.Join(operatorStr, words.Select(w => $"\"{EscapeFtsQuery(w)}\""));

        using var command = dbConnection.CreateCommand();
        command.CommandText = @"
            SELECT
                d.Id,
                d.FileId,
                d.Name,
                d.MimeType,
                d.Size,
                d.CreatedAt,
                d.ModifiedAt,
                d.Text,
                d.IndexedAt
            FROM documents_fts f
            JOIN documents d ON d.Id = f.rowid
            WHERE documents_fts MATCH @query
            ORDER BY rank
            LIMIT @limit";

        command.Parameters.AddWithValue("@query", ftsQuery);
        command.Parameters.AddWithValue("@limit", limit);

        var results = new List<SearchResult>();

        try
        {
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var document = new Document
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    FileId = reader.GetString(reader.GetOrdinal("FileId")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    MimeType = reader.GetString(reader.GetOrdinal("MimeType")),
                    Size = reader.GetInt64(reader.GetOrdinal("Size")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                    ModifiedAt = reader.IsDBNull(reader.GetOrdinal("ModifiedAt"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("ModifiedAt"))),
                    Text = reader.IsDBNull(reader.GetOrdinal("Text"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Text")),
                    IndexedAt = reader.IsDBNull(reader.GetOrdinal("IndexedAt"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("IndexedAt")))
                };

                var text = document.Text ?? "";
                var preview = text.Length > 200
                    ? text.Substring(0, 200).Replace("\n", " ")
                    : text;

                results.Add(new SearchResult
                {
                    Document = document,
                    Score = 0, // FTS rank not easily accessible, set to 0
                    Source = "fts",
                    Preview = preview
                });
            }
        }
        catch (SqliteException ex)
        {
            // FTS query syntax error - return empty results
            Console.WriteLine($"FTS query error: {ex.Message}");
            return new List<SearchResult>();
        }

        return results;
    }

    /// <summary>
    /// Escapes special characters in FTS query to prevent syntax errors.
    /// FTS5 special characters: " * ( ) [ ] { }
    /// </summary>
    private static string EscapeFtsQuery(string term)
    {
        // Remove or escape FTS5 special characters
        return term
            .Replace("\"", "")
            .Replace("*", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("{", "")
            .Replace("}", "");
    }
}
