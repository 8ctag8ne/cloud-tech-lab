using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using OpenAiRagDemo.Api.Controllers;
using OpenAiRagDemo.Api.Data;
using OpenAiRagDemo.Api.Services.Interfaces;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace OpenAiRagDemo.Api.Services;

public class RagService : IRagService
{
    private readonly AppDbContext _context;
    private readonly IOpenAiService _openAiService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        AppDbContext context,
        IOpenAiService openAiService,
        ILogger<RagService> logger)
    {
        _context = context;
        _openAiService = openAiService;
        _logger = logger;
    }

    /// <summary>
    /// Головний метод RAG: пошук + генерація відповіді
    /// </summary>
    public async Task<RagQueryResponse> QueryAsync(
        string question,
        Guid? bookId = null,
        int topK = 5,
        bool includeSources = true)
    {
        _logger.LogInformation("RAG Query: {Question}, BookId: {BookId}, TopK: {TopK}", 
            question, bookId, topK);

        // 1. Шукаємо релевантні чанки
        var similarChunks = await SearchSimilarChunksAsync(question, bookId, topK);

        if (!similarChunks.Any())
        {
            return new RagQueryResponse
            {
                Question = question,
                Answer = "I couldn't find relevant information in the knowledge base to answer this question.",
                Sources = includeSources ? new List<SearchResult>() : null,
                TotalChunksSearched = 0
            };
        }

        // 2. Формуємо контекст з найбільш релевантних чанків
        var context = BuildContext(similarChunks);

        // 3. Генеруємо відповідь через OpenAI
        var answer = await GenerateAnswerAsync(question, context);

        return new RagQueryResponse
        {
            Question = question,
            Answer = answer,
            Sources = includeSources ? similarChunks : null,
            TotalChunksSearched = similarChunks.Count
        };
    }

    /// <summary>
    /// Пошук схожих чанків через cosine similarity (pgvector)
    /// </summary>
    public async Task<List<SearchResult>> SearchSimilarChunksAsync(
        string query,
        Guid? bookId = null,
        int topK = 5)
    {
        // 1. Генеруємо embedding для запиту
        var queryEmbedding = await _openAiService.GenerateEmbeddingAsync(query);
        var queryVector = new Vector(queryEmbedding);

        // 2. Будуємо запит до БД
        var chunksQuery = _context.BookChunks
            .Include(c => c.Book)
            .Where(c => c.Embedding != null);

        // Фільтр по конкретній книзі (опціонально)
        if (bookId.HasValue)
        {
            chunksQuery = chunksQuery.Where(c => c.BookId == bookId.Value);
        }

        // 3. Виконуємо векторний пошук з cosine distance
        // Менша відстань = більша схожість
        var results = await chunksQuery
            .Select(c => new
            {
                Chunk = c,
                Distance = c.Embedding!.CosineDistance(queryVector)
            })
            .OrderBy(x => x.Distance)
            .Take(topK)
            .ToListAsync();

        // 4. Конвертуємо в SearchResult
        return results.Select(r => new SearchResult
        {
            ChunkId = r.Chunk.Id,
            BookId = r.Chunk.BookId,
            BookTitle = r.Chunk.Book?.Title ?? "Unknown",
            Content = r.Chunk.Content,
            ChunkIndex = r.Chunk.ChunkIndex,
            Similarity = 1 - r.Distance // Конвертуємо distance в similarity (0-1)
        }).ToList();
    }

    /// <summary>
    /// Будує контекст з релевантних чанків
    /// </summary>
    private string BuildContext(List<SearchResult> chunks)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("--- CONTEXT FROM KNOWLEDGE BASE ---");
        sb.AppendLine();
        
        for (int i = 0; i < chunks.Count; i++)
        {
            sb.AppendLine($"[Source {i + 1}: {chunks[i].BookTitle}, Chunk #{chunks[i].ChunkIndex}, Similarity: {chunks[i].Similarity:F2}]");
            sb.AppendLine(chunks[i].Content);
            sb.AppendLine();
            
            if (i < chunks.Count - 1)
            {
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Генерує відповідь через OpenAI Chat API на основі контексту
    /// </summary>
    private async Task<string> GenerateAnswerAsync(string question, string context)
    {
        var systemPrompt = """
            You are a helpful assistant that answers questions STRICTLY based on the provided context.
            
            IMPORTANT RULES:
            - Use ONLY information from the context provided
            - If the context contains relevant information, synthesize a comprehensive answer
            - Answer in the SAME LANGUAGE as the question
            - Be specific and provide concrete details from the context
            - Reference sources when possible (e.g., "According to Source 1...")
            - If you need to be uncertain, explain what information IS available in the context
            - Do NOT say "there is no information" unless the context is truly irrelevant
            
            Context may contain partial or indirect answers - use them constructively.
            """;

        var userPrompt = $"""
            Context:
            {context}

            Question: {question}

            Answer based on the context above:
            """;

        try
        {
            var answer = await _openAiService.GenerateChatResponseAsync(systemPrompt, userPrompt);
            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating answer");
            return "An error occurred while generating the answer.";
        }
    }
}