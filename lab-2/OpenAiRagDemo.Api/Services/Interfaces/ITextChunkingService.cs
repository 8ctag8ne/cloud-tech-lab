namespace OpenAiRagDemo.Api.Services.Interfaces;

public interface ITextChunkingService
{
    List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200);
}