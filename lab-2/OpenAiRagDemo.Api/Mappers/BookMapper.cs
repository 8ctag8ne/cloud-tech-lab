using OpenAiRagDemo.Api.DTOs;
using OpenAiRagDemo.Api.Models;

namespace OpenAiRagDemo.Api.Mappers;

public static class BookMapper
{
    /// <summary>
    /// Конвертує Book в BookDto
    /// </summary>
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
            ChunksCount = book.Chunks?.Count ?? 0
        };
    }

    /// <summary>
    /// Конвертує список Book в список BookDto
    /// </summary>
    public static List<BookDto> ToDto(this IEnumerable<Book> books)
    {
        return books.Select(b => b.ToDto()).ToList();
    }

    /// <summary>
    /// Створює Book з BookCreateDto
    /// </summary>
    public static Book ToEntity(this BookCreateDto dto, string filePath)
    {
        return new Book
        {
            Title = dto.Title,
            Authors = dto.Authors,
            Tags = dto.Tags,
            Description = dto.Description,
            FilePath = filePath
        };
    }

    /// <summary>
    /// Оновлює Book з BookUpdateDto
    /// </summary>
    public static void UpdateFromDto(this Book book, BookUpdateDto dto)
    {
        if (dto.Title != null) book.Title = dto.Title;
        if (dto.Authors != null) book.Authors = dto.Authors;
        if (dto.Tags != null) book.Tags = dto.Tags;
        if (dto.Description != null) book.Description = dto.Description;
    }
}