using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAiRagDemo.Api.Data;
using OpenAiRagDemo.Api.DTOs;
using OpenAiRagDemo.Api.Mappers;
using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        AppDbContext context, 
        IFileService fileService,
        ILogger<BooksController> logger)
    {
        _context = context;
        _fileService = fileService;
        _logger = logger;
    }

    // GET: api/books
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookDto>>> GetBooks()
    {
        var books = await _context.Books
            .Include(b => b.Chunks)
            .OrderByDescending(b => b.UploadedAt)
            .ToListAsync();

        return Ok(books.ToDto());
    }

    // GET: api/books/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<BookDto>> GetBook(Guid id)
    {
        var book = await _context.Books
            .Include(b => b.Chunks)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound(new { message = "Book not found" });
        }

        return Ok(book.ToDto());
    }

    // POST: api/books
    [HttpPost]
    public async Task<ActionResult<BookDto>> CreateBook([FromForm] BookCreateDto dto)
    {
        // Валідація файлу
        if (dto.File == null || dto.File.Length == 0)
        {
            return BadRequest(new { message = "File is required" });
        }

        if (!dto.File.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only PDF files are allowed" });
        }

        try
        {
            // Зберегти файл через сервіс
            var filePath = await _fileService.SaveFileAsync(dto.File, "books");

            // Створити запис в БД
            var book = dto.ToEntity(filePath);
            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Book created: {BookId}, File: {FilePath}", book.Id, filePath);

            return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book");
            return StatusCode(500, new { message = "Failed to create book", error = ex.Message });
        }
    }

    // PUT: api/books/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBook(Guid id, [FromBody] BookUpdateDto dto)
    {
        var book = await _context.Books.FindAsync(id);

        if (book == null)
        {
            return NotFound(new { message = "Book not found" });
        }

        // Оновити через mapper
        book.UpdateFromDto(dto);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Book updated: {BookId}", id);

        return NoContent();
    }

    // DELETE: api/books/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBook(Guid id)
    {
        var book = await _context.Books.FindAsync(id);

        if (book == null)
        {
            return NotFound(new { message = "Book not found" });
        }

        // Видалити файл через сервіс
        await _fileService.DeleteFileAsync(book.FilePath);

        // Видалити запис з БД
        _context.Books.Remove(book);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Book deleted: {BookId}", id);

        return NoContent();
    }

    // GET: api/books/{id}/download
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadBook(Guid id)
    {
        var book = await _context.Books.FindAsync(id);

        if (book == null)
        {
            return NotFound(new { message = "Book not found" });
        }

        if (!_fileService.FileExists(book.FilePath))
        {
            return NotFound(new { message = "File not found on disk" });
        }

        var stream = await _fileService.GetFileStreamAsync(book.FilePath);
        if (stream == null)
        {
            return StatusCode(500, new { message = "Failed to open file" });
        }

        var fileName = book.Title != null 
            ? $"{book.Title}.pdf" 
            : $"book_{book.Id}.pdf";

        return File(stream, "application/pdf", fileName);
    }
}