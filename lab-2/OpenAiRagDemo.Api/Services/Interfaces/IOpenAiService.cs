using System.Text.Json.Serialization;

namespace OpenAiRagDemo.Api.Services.Interfaces;

public interface IOpenAiService
{
    /// <summary>
    /// Тестовий запит до ChatGPT
    /// </summary>
    Task<string> TestChatAsync(string prompt);
    
    /// <summary>
    /// Генерує embedding для тексту
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text);
    
    /// <summary>
    /// Генерує embeddings для списку текстів
    /// </summary>
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);

    Task<string> GenerateChatResponseAsync(string systemPrompt, string userPrompt);
    
    /// <summary>
    /// Витягує метадані з тексту книги за допомогою GPT
    /// </summary>
    public Task<BookMetadata> ExtractMetadataFromTextAsync(string text);
    public Task<BookMetadata> ExtractMetadataFromPdfAsync(IFormFile pdfFile);
    public Task<string> ConvertPdfToMarkdownAsync(
        IFormFile pdfFile, 
        int maxPages = 10,
        int batchSize = 3);
    public Task<string> ExtractTextFromPdfAsync(IFormFile pdfFile, int maxPages = 10);
}

public class BookMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("authors")]
    public string? Authors { get; set; }
    
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}