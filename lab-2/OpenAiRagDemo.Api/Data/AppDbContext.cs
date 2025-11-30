using Microsoft.EntityFrameworkCore;
using OpenAiRagDemo.Api.Models;

namespace OpenAiRagDemo.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books { get; set; }
    public DbSet<BookChunk> BookChunks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Додаткові налаштування для pgvector
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasIndex(e => e.UploadedAt);
            
            entity.HasMany(e => e.Chunks)
                .WithOne(e => e.Book)
                .HasForeignKey(e => e.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookChunk>(entity =>
        {
            entity.HasIndex(e => e.BookId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}