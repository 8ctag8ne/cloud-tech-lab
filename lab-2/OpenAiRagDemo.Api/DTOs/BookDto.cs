namespace OpenAiRagDemo.Api.DTOs;

public class BookDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Tags { get; set; }
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    
    // НОВІ ПОЛЯ
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
    
    public int ChunksCount { get; set; }
}

public class BookCreateDto
{
    public required IFormFile File { get; set;}
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Tags { get; set; }
    public string? Description { get; set; }
}

public class BookUpdateDto
{
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Tags { get; set; }
    public string? Description { get; set; }
}