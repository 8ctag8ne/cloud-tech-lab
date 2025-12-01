using Microsoft.AspNetCore.Mvc;
using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly IRagService _ragService;
    private readonly ILogger<RagController> _logger;

    public RagController(IRagService ragService, ILogger<RagController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Запит до RAG-системи
    /// POST: api/rag/query
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<RagQueryResponse>> Query([FromBody] RagQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { message = "Question is required" });
        }

        try
        {
            _logger.LogInformation("Processing RAG query: {Question}", request.Question);

            var response = await _ragService.QueryAsync(
                request.Question,
                request.BookId,
                request.TopK,
                request.IncludeSources);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG query");
            return StatusCode(500, new { message = "Failed to process query", error = ex.Message });
        }
    }

    /// <summary>
    /// Пошук релевантних чанків без генерації відповіді
    /// POST: api/rag/search
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<List<SearchResult>>> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { message = "Query is required" });
        }

        try
        {
            var results = await _ragService.SearchSimilarChunksAsync(
                request.Query,
                request.BookId,
                request.TopK);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching chunks");
            return StatusCode(500, new { message = "Failed to search", error = ex.Message });
        }
    }
}

// DTOs
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