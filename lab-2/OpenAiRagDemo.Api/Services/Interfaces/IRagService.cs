using OpenAiRagDemo.Api.Controllers;

namespace OpenAiRagDemo.Api.Services.Interfaces;

public interface IRagService
{
    /// <summary>
    /// Обробляє запит через RAG: шукає релевантні чанки та генерує відповідь
    /// </summary>
    Task<RagQueryResponse> QueryAsync(
        string question, 
        Guid? bookId = null, 
        int topK = 5, 
        bool includeSources = true);

    /// <summary>
    /// Шукає схожі чанки без генерації відповіді
    /// </summary>
    Task<List<SearchResult>> SearchSimilarChunksAsync(
        string query, 
        Guid? bookId = null, 
        int topK = 5);
}