using DriveSearch.Core.Models;
using DriveSearch.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

namespace DriveSearch.Infrastructure.Search;

/// <summary>
/// Service for vector-based semantic search using sqlite-vec extension.
/// Performs cosine distance similarity search on document embeddings.
/// </summary>
public class VectorSearchService
{
    private readonly DriveSearchContext _context;
    private readonly double _maxDistance;

    /// <summary>
    /// Maximum cosine distance threshold for relevance (0.0 = identical, 2.0 = opposite)
    /// Results with distance > threshold are filtered out.
    /// </summary>
    public const double MaxDistanceThreshold = 0.40;

    public VectorSearchService(DriveSearchContext context)
    {
        _context = context;
        _maxDistance = MaxDistanceThreshold;
    }

    /// <summary>
    /// Performs vector similarity search using cosine distance.
    /// Returns documents ordered by relevance (lowest distance first).
    /// </summary>
    /// <param name="queryVector">Query embedding vector (float array)</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>List of search results with relevance scores</returns>
    public async Task<List<SearchResult>> SearchAsync(float[] queryVector, int limit = 5)
    {
        // Get connection with sqlite-vec extension loaded
        using var connection = await _context.GetVectorConnectionAsync();

        // Serialize query vector to binary BLOB
        var vectorBlob = SerializeVector(queryVector);

        // Query with cosine distance calculation
        // Fetch more candidates (3x limit) for filtering
        var candidateLimit = limit * 3;

        using var command = connection.CreateCommand();
        // Join document_chunks when the embedding is chunk-based (ChunkId not null).
        // COALESCE returns the chunk text for chunk embeddings, document text otherwise.
        command.CommandText = @"
            SELECT
                d.Id,
                d.FileId,
                d.Name,
                d.MimeType,
                d.Size,
                d.CreatedAt,
                d.ModifiedAt,
                d.IndexedAt,
                d.FolderPath,
                c.Text            AS ChunkText,
                COALESCE(c.Text, d.Text) AS MatchText,
                vec_distance_cosine(e.Vector, @vector) AS Distance
            FROM embeddings e
            JOIN documents d ON d.Id = e.DocumentId
            LEFT JOIN document_chunks c ON c.Id = e.ChunkId
            ORDER BY Distance ASC
            LIMIT @limit";

        command.Parameters.AddWithValue("@vector", vectorBlob);
        command.Parameters.AddWithValue("@limit", candidateLimit);

        var results = new List<SearchResult>();
        var seenDocuments = new HashSet<string>(); // deduplicate by FileId

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var distance = reader.GetDouble(reader.GetOrdinal("Distance"));

            if (distance >= _maxDistance)
                continue;

            var fileId = reader.GetString(reader.GetOrdinal("FileId"));

            // Keep only the best-scoring chunk per document
            if (!seenDocuments.Add(fileId))
                continue;

            var document = new Document
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                FileId = fileId,
                Name = reader.GetString(reader.GetOrdinal("Name")),
                MimeType = reader.GetString(reader.GetOrdinal("MimeType")),
                Size = reader.GetInt64(reader.GetOrdinal("Size")),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt"))
                    ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                ModifiedAt = reader.IsDBNull(reader.GetOrdinal("ModifiedAt"))
                    ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("ModifiedAt"))),
                IndexedAt = reader.IsDBNull(reader.GetOrdinal("IndexedAt"))
                    ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("IndexedAt"))),
                FolderPath = reader.IsDBNull(reader.GetOrdinal("FolderPath"))
                    ? null : reader.GetString(reader.GetOrdinal("FolderPath"))
            };

            var chunkText = reader.IsDBNull(reader.GetOrdinal("ChunkText"))
                ? null : reader.GetString(reader.GetOrdinal("ChunkText"));
            var matchText = reader.IsDBNull(reader.GetOrdinal("MatchText"))
                ? "" : reader.GetString(reader.GetOrdinal("MatchText"));

            var preview = matchText.Length > 200
                ? matchText.Substring(0, 200).Replace("\n", " ")
                : matchText.Replace("\n", " ");

            results.Add(new SearchResult
            {
                Document = document,
                Score = distance,
                Source = "vector",
                ChunkText = chunkText,
                Preview = preview
            });

            if (results.Count >= limit)
                break;
        }

        return results;
    }

    /// <summary>
    /// Serializes a float array to binary format (float32 array as byte array).
    /// Compatible with Python's struct.pack format.
    /// </summary>
    /// <param name="vector">Float array to serialize</param>
    /// <returns>Byte array representation</returns>
    public static byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Deserializes a binary BLOB back to float array.
    /// </summary>
    /// <param name="blob">Binary data</param>
    /// <returns>Float array</returns>
    public static float[] DeserializeVector(byte[] blob)
    {
        var floats = new float[blob.Length / sizeof(float)];
        Buffer.BlockCopy(blob, 0, floats, 0, blob.Length);
        return floats;
    }
}
