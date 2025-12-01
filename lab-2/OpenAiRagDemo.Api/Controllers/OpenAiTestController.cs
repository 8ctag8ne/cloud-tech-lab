// OpenAiTestController
using Microsoft.AspNetCore.Mvc;
using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpenAiTestController : ControllerBase
{
    private readonly IOpenAiService _openAiService;
    private readonly ILogger<OpenAiTestController> _logger;

    public OpenAiTestController(IOpenAiService openAiService, ILogger<OpenAiTestController> logger)
    {
        _openAiService = openAiService;
        _logger = logger;
    }

    [HttpGet("chat")]
    public async Task<IActionResult> TestChat([FromQuery] string prompt = "Say hello in 5 different languages")
    {
        try
        {
            var response = await _openAiService.TestChatAsync(prompt);
            return Ok(new { prompt, response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing chat");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("embedding")]
    public async Task<IActionResult> TestEmbedding([FromBody] TestEmbeddingRequest request)
    {
        try
        {
            var embedding = await _openAiService.GenerateEmbeddingAsync(request.Text);
            return Ok(new 
            { 
                text = request.Text,
                dimension = embedding.Length,
                embedding = embedding.Take(10).ToArray(),
                note = "Showing first 10 values only. Full dimension: " + embedding.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing embedding");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("metadata")]
    public async Task<IActionResult> TestMetadataExtraction([FromBody] TestEmbeddingRequest request)
    {
        try
        {
            var metadata = await _openAiService.ExtractMetadataFromTextAsync(request.Text);
            return Ok(new 
            { 
                input_length = request.Text.Length,
                metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing metadata extraction");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("full")]
    public async Task<IActionResult> FullTest()
    {
        try
        {
            var results = new Dictionary<string, object>();

            _logger.LogInformation("Testing chat...");
            var chatResponse = await _openAiService.TestChatAsync("Say 'API is working!' in a creative way");
            results["chat"] = new { success = true, response = chatResponse };

            _logger.LogInformation("Testing embedding...");
            var embedding = await _openAiService.GenerateEmbeddingAsync("This is a test sentence");
            results["embedding"] = new { success = true, dimension = embedding.Length };

            _logger.LogInformation("Testing metadata extraction...");
            var sampleText = """
                The Great Gatsby
                By F. Scott Fitzgerald

                In my younger and more vulnerable years my father gave me some advice that I've been turning over in my mind ever since.
                "Whenever you feel like criticizing any one," he told me, "just remember that all the people in this world haven't had the advantages that you've had."
                """;
            var metadata = await _openAiService.ExtractMetadataFromTextAsync(sampleText);
            results["metadata"] = new { success = true, metadata };

            return Ok(new 
            { 
                message = "All tests completed successfully!",
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in full test");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ----------------- Нові ендпоїнти -----------------

    /// <summary>
    /// Витягування метаданих прямо з PDF
    /// POST: api/openaitest/pdf-metadata
    /// FormData: pdfFile
    /// </summary>
    [HttpPost("pdf-metadata")]
    public async Task<IActionResult> ExtractPdfMetadata([FromForm] UploadFileDto dto)
    {
        if (dto.File == null)
            return BadRequest(new { error = "PDF file is required" });

        try
        {
            var metadata = await _openAiService.ExtractMetadataFromPdfAsync(dto.File);
            return Ok(new { fileName = dto.File.FileName, metadata });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from PDF");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Витягування тексту з PDF
    /// POST: api/openaitest/pdf-text
    /// FormData: pdfFile
    /// </summary>
    [HttpPost("pdf-markdown")]
    public async Task<IActionResult> ConvertPdfToMarkdown([FromForm] UploadFileDto dto, int pages = 5, int batchSize = 3)
    {
        if (dto.File == null)
            return BadRequest(new { error = "PDF file is required" });

        try
        {
            var text = await _openAiService.ConvertPdfToMarkdownAsync(dto.File, pages, batchSize);
            return Ok(new { fileName = dto.File.FileName, pages, text });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class TestEmbeddingRequest
{
    public string Text { get; set; } = string.Empty;
}

public class UploadFileDto
{
    public IFormFile File { get; set; } = null!;
}
