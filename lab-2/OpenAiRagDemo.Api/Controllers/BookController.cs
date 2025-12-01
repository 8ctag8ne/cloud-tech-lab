using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAiRagDemo.Api.Data;
using OpenAiRagDemo.Api.Models;
using OpenAiRagDemo.Api.DTOs;
using OpenAiRagDemo.Api.Mappers;
using OpenAiRagDemo.Api.Services.Interfaces;
using Pgvector;

namespace OpenAiRagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;
    private readonly IOpenAiService _openAiService;
    private readonly IPdfService _pdfService;
    private readonly ITextChunkingService _chunkingService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        AppDbContext context, 
        IFileService fileService,
        IOpenAiService openAiService,
        IPdfService pdfService,
        ITextChunkingService chunkingService,
        ILogger<BooksController> logger)
    {
        _context = context;
        _fileService = fileService;
        _openAiService = openAiService;
        _pdfService = pdfService;
        _chunkingService = chunkingService;
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
            // 1. Зберегти файл
            var filePath = await _fileService.SaveFileAsync(dto.File, "books");

            // 2. Прочитати PDF bytes
            byte[] pdfBytes;
            using (var stream = dto.File.OpenReadStream())
            {
                pdfBytes = await _pdfService.ReadAllBytesAsync(stream);
            }

            // 3. Витягнути метадані з PDF (якщо не передані вручну)
            BookMetadata? metadata = null;
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                _logger.LogInformation("Extracting metadata from PDF...");
                metadata = await _openAiService.ExtractMetadataFromPdfAsync(dto.File);
            }

            // 4. Створити запис в БД
            var book = dto.ToEntity(filePath);
            
            // Використати витягнуті метадані, якщо не передані вручну
            if (metadata != null)
            {
                book.Title ??= metadata.Title;
                book.Authors ??= metadata.Authors;
                book.Tags ??= metadata.Tags;
                book.Description ??= metadata.Description;
            }

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Book created: {BookId}, File: {FilePath}", book.Id, filePath);

            // 5. Автоматично обробити книгу (витягти текст, створити чанки, embeddings)
            try
            {
                await ProcessBookInternal(book, pdfBytes);
                _logger.LogInformation("Book automatically processed: {BookId}", book.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-process book {BookId}, can be processed later", book.Id);
                book.ProcessingError = $"Auto-processing failed: {ex.Message}";
                await _context.SaveChangesAsync();
            }

            // Перезавантажити книгу з чанками
            await _context.Entry(book).Collection(b => b.Chunks).LoadAsync();

            return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book");
            return StatusCode(500, new { message = "Failed to create book", error = ex.Message });
        }
    }

    // POST: api/books/{id}/process
    [HttpPost("{id}/process")]
    public async Task<ActionResult<BookDto>> ProcessBook(
        Guid id, 
        [FromQuery] int chunkSize = 1000, 
        [FromQuery] int overlap = 200)
    {
        var book = await _context.Books
            .Include(b => b.Chunks)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound(new { message = "Book not found" });
        }

        if (book.IsProcessed)
        {
            return BadRequest(new { message = "Book is already processed" });
        }

        try
        {
            _logger.LogInformation("Processing book: {BookId}", id);

            // Прочитати PDF bytes
            var fileStream = await _fileService.GetFileStreamAsync(book.FilePath);
            if (fileStream == null)
            {
                return StatusCode(500, new { message = "Failed to open PDF file" });
            }

            var pdfBytes = await _pdfService.ReadAllBytesAsync(fileStream);
            await fileStream.DisposeAsync();

            await ProcessBookInternal(book, pdfBytes, chunkSize, overlap);

            // Перезавантажити книгу з чанками
            await _context.Entry(book).Collection(b => b.Chunks).LoadAsync();

            return Ok(book.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing book: {BookId}", id);
            
            book.ProcessingError = ex.Message;
            await _context.SaveChangesAsync();

            return StatusCode(500, new { message = "Failed to process book", error = ex.Message });
        }
    }

    /// <summary>
    /// Внутрішній метод для обробки книги (витягування тексту, чанкування, embeddings)
    /// </summary>
    private async Task ProcessBookInternal(
        Book book, 
        byte[] pdfBytes, 
        int chunkSize = 1000, 
        int overlap = 200)
    {
        // 1. Визначити кількість сторінок
        int totalPages = _pdfService.GetPageCount(pdfBytes);
        _logger.LogInformation("PDF has {TotalPages} pages", totalPages);

        // 2. Витягти текст з усіх сторінок
        var text = _pdfService.ExtractText(pdfBytes, totalPages);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Failed to extract text from PDF");
        }

        // 3. Розбити текст на чанки (по словах)
        var chunks = _chunkingService.ChunkText(text, chunkSize, overlap);
        _logger.LogInformation("Created {Count} chunks", chunks.Count);

        // 4. Генерувати embeddings для всіх чанків (батч)
        var embeddings = await _openAiService.GenerateEmbeddingsAsync(chunks);

        // 5. Видалити старі чанки, якщо є (для перепроцесингу)
        if (book.Chunks.Any())
        {
            _context.BookChunks.RemoveRange(book.Chunks);
        }

        // 6. Зберегти нові чанки в БД
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = new BookChunk
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                Content = chunks[i],
                ChunkIndex = i,
                Embedding = new Vector(embeddings[i]),
                CreatedAt = DateTime.UtcNow
            };

            _context.BookChunks.Add(chunk);
        }

        // 7. Оновити статус книги
        book.IsProcessed = true;
        book.ProcessedAt = DateTime.UtcNow;
        book.ProcessingError = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully processed book: {BookId}, Chunks: {Count}", book.Id, chunks.Count);
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

        // Видалити файл
        await _fileService.DeleteFileAsync(book.FilePath);

        // Видалити запис з БД (chunks видаляться автоматично через cascade)
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