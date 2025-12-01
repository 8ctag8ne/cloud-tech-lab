// BookMappers.cs
using OpenAiRagDemo.Api.DTOs;
using OpenAiRagDemo.Api.Models;

namespace OpenAiRagDemo.Api.Mappers;

public static class BookMappers
{
    public static BookDto ToDto(this Book book)
    {
        return new BookDto
        {
            Id = book.Id,
            Title = book.Title,
            Authors = book.Authors,
            Tags = book.Tags,
            Description = book.Description,
            FilePath = book.FilePath,
            UploadedAt = book.UploadedAt,
            IsProcessed = book.IsProcessed,
            ProcessedAt = book.ProcessedAt,
            ProcessingError = book.ProcessingError,
            ChunksCount = book.Chunks?.Count ?? 0
        };
    }

    public static IEnumerable<BookDto> ToDto(this IEnumerable<Book> books)
    {
        return books.Select(b => b.ToDto());
    }

    public static Book ToEntity(this BookCreateDto dto, string filePath)
    {
        return new Book
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Authors = dto.Authors,
            Tags = dto.Tags,
            Description = dto.Description,
            FilePath = filePath,
            UploadedAt = DateTime.UtcNow,
            IsProcessed = false
        };
    }

    public static void UpdateFromDto(this Book book, BookUpdateDto dto)
    {
        book.Title = dto.Title;
        book.Authors = dto.Authors;
        book.Tags = dto.Tags;
        book.Description = dto.Description;
    }
}