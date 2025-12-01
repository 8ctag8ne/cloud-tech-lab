using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace OpenAiRagDemo.Api.Models;

[Table("book_chunks")]
public class BookChunk
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("book_id")]
    [Required]
    public Guid BookId { get; set; }

    [Column("content")]
    [Required]
    public string Content { get; set; } = string.Empty;

    [Column("chunk_index")]
    public int? ChunkIndex { get; set; }

    [Column("embedding")]
    public Vector? Embedding { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("BookId")]
    public Book Book { get; set; } = null!;
}