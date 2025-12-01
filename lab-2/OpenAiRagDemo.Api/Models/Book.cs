using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenAiRagDemo.Api.Models;

[Table("books")]
public class Book
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("title")]
    [MaxLength(500)]
    public string? Title { get; set; }

    [Column("authors")]
    [MaxLength(1000)]
    public string? Authors { get; set; }

    [Column("tags")]
    [MaxLength(1000)]
    public string? Tags { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("file_path")]
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Column("is_processed")]
    public bool IsProcessed { get; set; } = false;
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
    [Column("processing_error")]
    public string? ProcessingError { get; set; }

    // Navigation property
    public ICollection<BookChunk> Chunks { get; set; } = new List<BookChunk>();
}