//OpenAiRagDemo.Api/Services/OpenAiService.cs
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Services;

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIClient _client;
    private readonly IPdfService _pdfService;
    private readonly string _embeddingModel;
    private readonly string _chatModel;
    private readonly string _visionModel;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(
        IConfiguration configuration, 
        IPdfService pdfService,
        ILogger<OpenAiService> logger)
    {
        _logger = logger;
        _pdfService = pdfService;

        var apiKey = configuration["OPENAI_API_KEY"]
                 ?? throw new InvalidOperationException("OpenAI API key missing");

        _embeddingModel = configuration["OPENAI_EMBEDDING_MODEL"] ?? "text-embedding-3-small";
        _chatModel = configuration["OPENAI_CHAT_MODEL"] ?? "gpt-4o-mini";
        _visionModel = configuration["OPENAI_VISION_MODEL"] ?? "gpt-4o";

        _client = new OpenAIClient(apiKey);
    }

    public async Task<string> TestChatAsync(string prompt)
    {
        var chatClient = _client.GetChatClient(_chatModel);
        var completion = await chatClient.CompleteChatAsync(prompt);
        return completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : string.Empty;
    }
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var emb = _client.GetEmbeddingClient(_embeddingModel);
        
        // Затримка залежно від розміру тексту (щоб уникнути rate limits)
        await ApplyRateLimitDelay(text.Length);
        
        var result = await emb.GenerateEmbeddingAsync(text);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        var emb = _client.GetEmbeddingClient(_embeddingModel);
        var result = new List<float[]>();
        
        // Розраховуємо загальну кількість символів у батчі
        int totalChars = texts.Sum(t => t.Length);
        int batchLength = 100000;
        for(int i = 0; i < texts.Count; i++)
        {
            int currentLength = 0;
            int j = i;
            List<string> batch = new List<string>();
            for(; j < texts.Count; j++)
            {
                if(texts[j].Length + currentLength > batchLength)
                {
                    break;
                }
                batch.Add(texts[j]);
                currentLength += texts[j].Length;
            }
            i = j - 1;
            var embeddings = await emb.GenerateEmbeddingsAsync(batch);
            result.AddRange([.. embeddings.Value.Select(x => x.ToFloats().ToArray())]);
            await ApplyRateLimitDelay(currentLength);
            currentLength = 0;
        }

        return result;
    }

    /// <summary>
    /// Застосовує затримку для уникнення rate limits OpenAI
    /// </summary>
    private async Task ApplyRateLimitDelay(int characterCount)
    {
        // Базова затримка: 100ms на кожні 1000 символів
        // Мінімум: 50ms, Максимум: 2000ms
        int delayMs = Math.Clamp(characterCount / 100, 50, 1000);
        
        _logger.LogDebug("Applying rate limit delay: {DelayMs}ms for {CharCount} characters", 
            delayMs, characterCount);
        
        await Task.Delay(delayMs);
    }

    /// <summary>
    /// Конвертує PDF в Markdown через Vision API
    /// </summary>
    public async Task<string> ConvertPdfToMarkdownAsync(
        IFormFile pdfFile, 
        int maxPages = 10,
        int batchSize = 3)
    {
        try
        {
            _logger.LogInformation("Converting PDF to Markdown: {FileName}", pdfFile.FileName);

            // 1. Конвертуємо PDF в PNG bytes
            byte[] pdfBytes;
            using (var stream = pdfFile.OpenReadStream())
            {
                pdfBytes = await _pdfService.ReadAllBytesAsync(stream);
            }

            var pageImages = await _pdfService.ConvertPdfToPngBytesAsync(pdfBytes, maxPages);
            _logger.LogInformation("Converted {Count} pages to images", pageImages.Count);

            // 2. Обробляємо сторінки батчами
            var chatClient = _client.GetChatClient(_visionModel);
            var markdownBuilder = new StringBuilder();
            
            markdownBuilder.AppendLine($"# {Path.GetFileNameWithoutExtension(pdfFile.FileName)}");
            markdownBuilder.AppendLine();

            for (int batchStart = 0; batchStart < pageImages.Count; batchStart += batchSize)
            {
                int batchEnd = Math.Min(batchStart + batchSize, pageImages.Count);
                int currentBatchSize = batchEnd - batchStart;

                _logger.LogInformation("Processing pages {Start}-{End} of {Total}", 
                    batchStart + 1, batchEnd, pageImages.Count);

                var contentParts = new List<ChatMessageContentPart>
                {
                    ChatMessageContentPart.CreateTextPart($"""
                        Convert these {currentBatchSize} PDF page(s) to Markdown format.
                        
                        Requirements:
                        - Extract ALL text accurately
                        - Preserve structure: headings, lists, tables
                        - Use proper Markdown syntax
                        - For tables, use Markdown table format
                        - Separate pages with: <!-- Page X -->
                        - NO commentary, ONLY Markdown content
                        
                        Page(s) {batchStart + 1} to {batchEnd}:
                        """)
                };

                // Додаємо зображення з batch
                for (int i = batchStart; i < batchEnd; i++)
                {
                    contentParts.Add(ChatMessageContentPart.CreateImagePart(
                        BinaryData.FromBytes(pageImages[i]), 
                        "image/png"));
                }

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(contentParts.ToArray())
                };

                var completion = await chatClient.CompleteChatAsync(messages);
                var markdown = completion.Value.Content.Count > 0 
                    ? completion.Value.Content[0].Text 
                    : string.Empty;

                if (markdown.StartsWith("```"))
                markdown = markdown.Replace("```markdown", "").Replace("```", "").Trim();

                markdownBuilder.AppendLine(markdown.Trim());
                markdownBuilder.AppendLine();

                // Затримка між батчами
                if (batchEnd < pageImages.Count)
                {
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Successfully converted PDF to Markdown");
            return markdownBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting PDF to Markdown");
            throw;
        }
    }

    /// <summary>
    /// Витягує метадані з тексту
    /// </summary>
    public async Task<BookMetadata> ExtractMetadataFromTextAsync(string text)
    {
        try
        {
            var chatClient = _client.GetChatClient(_chatModel);

            // Беремо перші 4000 символів для аналізу
            var sample = text.Length > 10000 ? text[..10000] : text;

            var prompt = """
                Analyze this text sample and extract book metadata.
                Return ONLY valid JSON with these fields: title, authors, tags, description.
                If you cannot determine a field, use null.
                Tags should be comma-separated keywords.

                Text:
                "{sample}"

                Return format:
                {{
                    "title": "Title or null",
                    "authors": "Author(s) or null",
                    "tags": "tag1, tag2, tag3 or null",
                    "description": "Description up to 200 words or null"
                }}
                """;
            prompt = prompt.Replace("{sample}", sample);

            var completion = await chatClient.CompleteChatAsync(prompt);
            var json = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text.Trim() : "{}";

            // Очищення JSON
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            return JsonSerializer.Deserialize<BookMetadata>(json) ?? new BookMetadata();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from text");
            return new BookMetadata();
        }
    }

    /// <summary>
    /// Витягує метадані з PDF (використовує Markdown конвертацію)
    /// </summary>
    public async Task<BookMetadata> ExtractMetadataFromPdfAsync(IFormFile pdfFile)
    {
        try
        {
            _logger.LogInformation("Extracting metadata from PDF: {FileName}", pdfFile.FileName);

            // Конвертуємо першу сторінку в Markdown
            var markdown = await ConvertPdfToMarkdownAsync(pdfFile, maxPages: 6, batchSize: 3);

            // Витягуємо метадані з тексту
            var metadata = await ExtractMetadataFromTextAsync(markdown);

            _logger.LogInformation("Successfully extracted metadata: {Title}", metadata.Title);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from PDF");
            return new BookMetadata();
        }
    }

    /// <summary>
    /// Витягує текст з PDF (без Vision API)
    /// </summary>
    public async Task<string> ExtractTextFromPdfAsync(IFormFile pdfFile, int maxPages = 10)
    {
        try
        {
            byte[] pdfBytes;
            using (var stream = pdfFile.OpenReadStream())
            {
                pdfBytes = await _pdfService.ReadAllBytesAsync(stream);
            }

            var text = _pdfService.ExtractText(pdfBytes, maxPages);
            _logger.LogInformation("Extracted {Length} characters from PDF", text.Length);
            
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            return string.Empty;
        }
    }

    public async Task<string> GenerateChatResponseAsync(string systemPrompt, string userPrompt)
    {
        var chatClient = _client.GetChatClient(_chatModel);
        
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var completion = await chatClient.CompleteChatAsync(messages);
        return completion.Value.Content.Count > 0 
            ? completion.Value.Content[0].Text 
            : string.Empty;
    }
}