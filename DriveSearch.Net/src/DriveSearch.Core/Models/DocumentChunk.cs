namespace DriveSearch.Core.Models;

public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public required string Text { get; set; }
    public Document? Document { get; set; }
}
