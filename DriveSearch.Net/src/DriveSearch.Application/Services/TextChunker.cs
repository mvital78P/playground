namespace DriveSearch.Application.Services;

public static class TextChunker
{
    /// <summary>
    /// Splits text into overlapping chunks, preferring paragraph and sentence boundaries.
    /// </summary>
    /// <param name="text">Source text</param>
    /// <param name="chunkSize">Target chunk size in characters (~300-400 tokens)</param>
    /// <param name="overlap">Overlap between consecutive chunks for context continuity</param>
    /// <param name="minChunkSize">Chunks smaller than this are dropped</param>
    public static List<string> Split(string text, int chunkSize = 1500, int overlap = 200, int minChunkSize = 100)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        text = text.Trim();
        int start = 0;

        while (start < text.Length)
        {
            int end = Math.Min(start + chunkSize, text.Length);

            if (end < text.Length)
            {
                // Prefer breaking at a paragraph boundary within the last 300 chars of the window
                int lookback = Math.Min(300, end - start);
                int paraBreak = text.LastIndexOf('\n', end - 1, lookback);
                if (paraBreak > start + chunkSize / 2)
                {
                    end = paraBreak;
                }
                else
                {
                    // Fall back to sentence boundary (". ")
                    int sentBreak = text.LastIndexOf(". ", end - 1, lookback);
                    if (sentBreak > start + chunkSize / 2)
                        end = sentBreak + 1; // include the period
                }
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length >= minChunkSize)
                chunks.Add(chunk);

            if (end >= text.Length) break;
            start = Math.Max(start + 1, end - overlap);
        }

        return chunks;
    }
}
