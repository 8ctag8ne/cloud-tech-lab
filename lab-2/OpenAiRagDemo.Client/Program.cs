using System.Net.Http.Json;
using System.Text.Json;

namespace OpenAiRagDemo.Client;

class Program
{
    private static readonly HttpClient _httpClient = new();
    private static string _baseUrl = "http://localhost:5278"; // Змініть на свій порт
    
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        // Перевірка з'єднання
        if (!await CheckConnection())
        {
            WriteError("Cannot connect to API. Make sure the server is running.");
            Console.ReadKey();
            return;
        }

        WriteSuccess("Connected to RAG API");
        Console.WriteLine();

        bool exit = false;
        while (!exit)
        {
            ShowMainMenu();
            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await ListBooks();
                        break;
                    case "2":
                        await AddBook();
                        break;
                    case "3":
                        await ViewBook();
                        break;
                    case "4":
                        await DeleteBook();
                        break;
                    case "5":
                        await GenerateMarkdown();
                        break;
                    case "6":
                        await AskQuestion();
                        break;
                    case "7":
                        await AskQuestionToBook();
                        break;
                    case "8":
                        await SearchChunks();
                        break;
                    case "9":
                        await ProcessBook();
                        break;
                    case "0":
                        exit = true;
                        WriteInfo("Goodbye!");
                        break;
                    default:
                        WriteWarning("Invalid choice. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error: {ex.Message}");
            }

            if (!exit)
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }
    }

    static void ShowMainMenu()
    {
        Console.Clear();
        WriteTitle("╔══════════════════════════════════════════╗");
        WriteTitle("║      RAG KNOWLEDGE BASE MANAGER          ║");
        WriteTitle("╚══════════════════════════════════════════╝");
        Console.WriteLine();
        
        Console.WriteLine("📚 Books Management:");
        Console.WriteLine("  1. List all books");
        Console.WriteLine("  2. Add book from file");
        Console.WriteLine("  3. View book details");
        Console.WriteLine("  4. Delete book");
        Console.WriteLine();
        
        Console.WriteLine("📄 Document Processing:");
        Console.WriteLine("  5. Generate Markdown from PDF");
        Console.WriteLine("  9. Process/Reprocess book");
        Console.WriteLine();
        
        Console.WriteLine("🤖 RAG Queries:");
        Console.WriteLine("  6. Ask question (all books)");
        Console.WriteLine("  7. Ask question (specific book)");
        Console.WriteLine("  8. Search similar chunks");
        Console.WriteLine();
        
        Console.WriteLine("  0. Exit");
        Console.WriteLine();
        Console.Write("Choose an option: ");
    }

    static async Task<bool> CheckConnection()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/books");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    static async Task ListBooks()
    {
        WriteHeader("📚 Books in Database");
        
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/books");
        response.EnsureSuccessStatusCode();
        
        var books = await response.Content.ReadFromJsonAsync<List<BookDto>>();
        
        if (books == null || !books.Any())
        {
            WriteWarning("No books found.");
            return;
        }

        foreach (var book in books)
        {
            Console.WriteLine();
            Console.WriteLine($"─────────────────────────────────────────");
            WriteInfo($"ID: {book.Id}");
            Console.WriteLine($"Title: {book.Title ?? "Untitled"}");
            Console.WriteLine($"Authors: {book.Authors ?? "Unknown"}");
            Console.WriteLine($"Tags: {book.Tags ?? "None"}");
            Console.WriteLine($"Uploaded: {book.UploadedAt:yyyy-MM-dd HH:mm}");
            
            if (book.IsProcessed)
            {
                WriteSuccess($"✓ Processed ({book.ChunkCount} chunks)");
            }
            else
            {
                WriteWarning("⚠ Not processed yet");
                if (!string.IsNullOrEmpty(book.ProcessingError))
                {
                    WriteError($"  Error: {book.ProcessingError}");
                }
            }
        }
        
        Console.WriteLine($"\n─────────────────────────────────────────");
        WriteInfo($"Total: {books.Count} books");
    }

    static async Task AddBook()
    {
        WriteHeader("➕ Add New Book");
        
        Console.Write("Enter PDF file path: ");
        var filePath = Console.ReadLine()?.Trim().Trim('"');
        
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            WriteError("File not found!");
            return;
        }

        Console.Write("Title (optional, press Enter to auto-detect): ");
        var title = Console.ReadLine()?.Trim();
        
        Console.Write("Authors (optional): ");
        var authors = Console.ReadLine()?.Trim();
        
        Console.Write("Tags (optional, comma-separated): ");
        var tags = Console.ReadLine()?.Trim();

        WriteInfo("Uploading file...");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", Path.GetFileName(filePath));
        
        if (!string.IsNullOrEmpty(title))
            content.Add(new StringContent(title), "Title");
        if (!string.IsNullOrEmpty(authors))
            content.Add(new StringContent(authors), "Authors");
        if (!string.IsNullOrEmpty(tags))
            content.Add(new StringContent(tags), "Tags");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/books", content);
        
        if (response.IsSuccessStatusCode)
        {
            var book = await response.Content.ReadFromJsonAsync<BookDto>();
            WriteSuccess($"✓ Book added successfully! ID: {book?.Id}");
            
            if (book?.IsProcessed == true)
            {
                WriteSuccess($"✓ Book automatically processed ({book.ChunkCount} chunks created)");
            }
            else
            {
                WriteWarning("⚠ Book added but not processed yet. Use option 9 to process it.");
            }
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            WriteError($"Failed to add book: {error}");
        }
    }

    static async Task ViewBook()
    {
        WriteHeader("👁 View Book Details");
        
        Console.Write("Enter Book ID: ");
        var idStr = Console.ReadLine();
        
        if (!Guid.TryParse(idStr, out var id))
        {
            WriteError("Invalid ID format!");
            return;
        }

        var response = await _httpClient.GetAsync($"{_baseUrl}/api/books/{id}");
        
        if (!response.IsSuccessStatusCode)
        {
            WriteError("Book not found!");
            return;
        }

        var book = await response.Content.ReadFromJsonAsync<BookDto>();
        
        if (book == null) return;

        Console.WriteLine();
        Console.WriteLine($"╔═══════════════════════════════════════════════════╗");
        Console.WriteLine($"  Title: {book.Title ?? "Untitled"}");
        Console.WriteLine($"  Authors: {book.Authors ?? "Unknown"}");
        Console.WriteLine($"  Tags: {book.Tags ?? "None"}");
        Console.WriteLine($"╚═══════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        if (!string.IsNullOrEmpty(book.Description))
        {
            Console.WriteLine("Description:");
            Console.WriteLine(book.Description);
            Console.WriteLine();
        }
        
        Console.WriteLine($"ID: {book.Id}");
        Console.WriteLine($"Uploaded: {book.UploadedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"File: {book.FilePath}");
        Console.WriteLine();
        
        if (book.IsProcessed)
        {
            WriteSuccess($"✓ Processed on {book.ProcessedAt:yyyy-MM-dd HH:mm:ss}");
            WriteInfo($"  Chunks: {book.ChunkCount}");
        }
        else
        {
            WriteWarning("⚠ Not processed");
            if (!string.IsNullOrEmpty(book.ProcessingError))
            {
                WriteError($"Error: {book.ProcessingError}");
            }
        }
    }

    static async Task DeleteBook()
    {
        WriteHeader("🗑 Delete Book");
        
        Console.Write("Enter Book ID: ");
        var idStr = Console.ReadLine();
        
        if (!Guid.TryParse(idStr, out var id))
        {
            WriteError("Invalid ID format!");
            return;
        }

        Console.Write("Are you sure? (yes/no): ");
        var confirm = Console.ReadLine()?.ToLower();
        
        if (confirm != "yes" && confirm != "y")
        {
            WriteInfo("Cancelled.");
            return;
        }

        var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/books/{id}");
        
        if (response.IsSuccessStatusCode)
        {
            WriteSuccess("✓ Book deleted successfully!");
        }
        else
        {
            WriteError("Failed to delete book. Make sure the ID is correct.");
        }
    }

    static async Task GenerateMarkdown()
    {
        WriteHeader("📄 Generate Markdown from PDF");
        
        Console.Write("Enter PDF file path: ");
        var filePath = Console.ReadLine()?.Trim().Trim('"');
        
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            WriteError("File not found!");
            return;
        }

        Console.Write("Number of pages to convert (default 5): ");
        var pagesStr = Console.ReadLine();
        int pages = string.IsNullOrEmpty(pagesStr) ? 5 : int.Parse(pagesStr);
        
        Console.Write("Batch size (default 3): ");
        var batchStr = Console.ReadLine();
        int batchSize = string.IsNullOrEmpty(batchStr) ? 3 : int.Parse(batchStr);

        WriteInfo($"Converting {pages} pages (this may take a while)...");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/openaitest/pdf-markdown?pages={pages}&batchSize={batchSize}", 
            content);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<MarkdownResponse>();
            
            if (result != null)
            {
                var outputPath = Path.ChangeExtension(filePath, ".md");
                await File.WriteAllTextAsync(outputPath, result.Text);
                
                WriteSuccess($"✓ Markdown saved to: {outputPath}");
                WriteInfo($"  Length: {result.Text.Length} characters");
            }
        }
        else
        {
            WriteError("Failed to convert PDF to Markdown");
        }
    }

    static async Task AskQuestion()
    {
        WriteHeader("🤖 Ask Question (All Books)");
        
        Console.Write("Your question: ");
        var question = Console.ReadLine();
        
        if (string.IsNullOrEmpty(question))
        {
            WriteWarning("Question cannot be empty!");
            return;
        }

        Console.Write("Number of relevant chunks to use (default 5): ");
        var topKStr = Console.ReadLine();
        int topK = string.IsNullOrEmpty(topKStr) ? 5 : int.Parse(topKStr);

        WriteInfo("Searching and generating answer...");
        Console.WriteLine();

        var request = new RagQueryRequest
        {
            Question = question,
            TopK = topK,
            IncludeSources = true
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/rag/query", request);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RagQueryResponse>();
            
            if (result != null)
            {
                DisplayRagResponse(result);
            }
        }
        else
        {
            WriteError("Failed to get answer");
        }
    }

    static async Task AskQuestionToBook()
    {
        WriteHeader("🤖 Ask Question (Specific Book)");
        
        Console.Write("Enter Book ID: ");
        var idStr = Console.ReadLine();
        
        if (!Guid.TryParse(idStr, out var bookId))
        {
            WriteError("Invalid ID format!");
            return;
        }

        Console.Write("Your question: ");
        var question = Console.ReadLine();
        
        if (string.IsNullOrEmpty(question))
        {
            WriteWarning("Question cannot be empty!");
            return;
        }

        Console.Write("Number of relevant chunks (default 5): ");
        var topKStr = Console.ReadLine();
        int topK = string.IsNullOrEmpty(topKStr) ? 5 : int.Parse(topKStr);

        WriteInfo("Searching and generating answer...");
        Console.WriteLine();

        var request = new RagQueryRequest
        {
            Question = question,
            BookId = bookId,
            TopK = topK,
            IncludeSources = true
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/rag/query", request);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RagQueryResponse>();
            
            if (result != null)
            {
                DisplayRagResponse(result);
            }
        }
        else
        {
            WriteError("Failed to get answer");
        }
    }

    static async Task SearchChunks()
    {
        WriteHeader("🔍 Search Similar Chunks");
        
        Console.Write("Search query: ");
        var query = Console.ReadLine();
        
        if (string.IsNullOrEmpty(query))
        {
            WriteWarning("Query cannot be empty!");
            return;
        }

        Console.Write("Book ID (optional, press Enter for all books): ");
        var idStr = Console.ReadLine();
        Guid? bookId = string.IsNullOrEmpty(idStr) ? null : Guid.Parse(idStr);

        Console.Write("Number of results (default 5): ");
        var topKStr = Console.ReadLine();
        int topK = string.IsNullOrEmpty(topKStr) ? 5 : int.Parse(topKStr);

        var request = new SearchRequest
        {
            Query = query,
            BookId = bookId,
            TopK = topK
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/rag/search", request);
        
        if (response.IsSuccessStatusCode)
        {
            var results = await response.Content.ReadFromJsonAsync<List<SearchResult>>();
            
            if (results != null && results.Any())
            {
                Console.WriteLine($"\nFound {results.Count} relevant chunks:\n");
                
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Console.WriteLine($"[{i + 1}] Similarity: {result.Similarity:F3}");
                    WriteInfo($"    Book: {result.BookTitle} (Chunk #{result.ChunkIndex})");
                    Console.WriteLine($"    {TruncateText(result.Content, 200)}");
                    Console.WriteLine();
                }
            }
            else
            {
                WriteWarning("No results found.");
            }
        }
        else
        {
            WriteError("Search failed");
        }
    }

    static async Task ProcessBook()
    {
        WriteHeader("⚙ Process Book");
        
        Console.Write("Enter Book ID: ");
        var idStr = Console.ReadLine();
        
        if (!Guid.TryParse(idStr, out var id))
        {
            WriteError("Invalid ID format!");
            return;
        }

        Console.Write("Chunk size (default 1000): ");
        var chunkStr = Console.ReadLine();
        int chunkSize = string.IsNullOrEmpty(chunkStr) ? 1000 : int.Parse(chunkStr);
        
        Console.Write("Overlap (default 200): ");
        var overlapStr = Console.ReadLine();
        int overlap = string.IsNullOrEmpty(overlapStr) ? 200 : int.Parse(overlapStr);

        WriteInfo("Processing book (this may take a while)...");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/books/{id}/process?chunkSize={chunkSize}&overlap={overlap}", 
            null);
        
        if (response.IsSuccessStatusCode)
        {
            var book = await response.Content.ReadFromJsonAsync<BookDto>();
            WriteSuccess($"✓ Book processed successfully!");
            WriteInfo($"  Created {book?.ChunkCount} chunks");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            WriteError($"Processing failed: {error}");
        }
    }

    static void DisplayRagResponse(RagQueryResponse response)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════╗");
        WriteTitle($"  Question: {response.Question}");
        Console.WriteLine("╚════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Answer:");
        Console.ResetColor();
        Console.WriteLine(response.Answer);
        Console.WriteLine();
        
        if (response.Sources != null && response.Sources.Any())
        {
            WriteInfo($"Based on {response.TotalChunksSearched} chunks from:");
            Console.WriteLine();
            
            foreach (var source in response.Sources.Take(5))
            {
                Console.WriteLine($"  • {source.BookTitle}");
                Console.WriteLine($"    Chunk #{source.ChunkIndex}, Similarity: {source.Similarity:F3}");
                Console.WriteLine($"    Preview: {TruncateText(source.Content, 150)}");
                Console.WriteLine();
            }
        }
    }

    static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    static void WriteHeader(string text)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(text);
        Console.WriteLine(new string('─', text.Length));
        Console.ResetColor();
    }

    static void WriteTitle(string text)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    static void WriteSuccess(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    static void WriteWarning(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    static void WriteInfo(string text)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}

// DTOs
public class BookDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Tags { get; set; }
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
    public int ChunkCount { get; set; }
}

public class RagQueryRequest
{
    public string Question { get; set; } = string.Empty;
    public Guid? BookId { get; set; }
    public int TopK { get; set; } = 5;
    public bool IncludeSources { get; set; } = true;
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public Guid? BookId { get; set; }
    public int TopK { get; set; } = 5;
}

public class RagQueryResponse
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<SearchResult>? Sources { get; set; }
    public int TotalChunksSearched { get; set; }
}

public class SearchResult
{
    public Guid ChunkId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? ChunkIndex { get; set; }
    public double Similarity { get; set; }
}

public class MarkdownResponse
{
    public string FileName { get; set; } = string.Empty;
    public int Pages { get; set; }
    public string Text { get; set; } = string.Empty;
}