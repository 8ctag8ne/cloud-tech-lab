// TextChunkingService.cs
using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Services;

public class TextChunkingService : ITextChunkingService
{
    /// <summary>
    /// Розбиває текст на чанки по словах з перекриттям
    /// </summary>
    public List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200)
    {
        var chunks = new List<string>();
        
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // Розбиваємо текст на слова
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length == 0)
            return chunks;

        var currentChunk = new List<string>();
        int currentLength = 0;

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var wordLength = word.Length + 1; // +1 для пробілу

            // Якщо додавання слова перевищує розмір чанку
            if (currentLength + wordLength > chunkSize && currentChunk.Count > 0)
            {
                // Зберігаємо поточний чанк
                chunks.Add(string.Join(" ", currentChunk));

                // Визначаємо overlap у словах
                int overlapLength = 0;
                var overlapWords = new List<string>();
                
                for (int j = currentChunk.Count - 1; j >= 0; j--)
                {
                    var overlapWord = currentChunk[j];
                    if (overlapLength + overlapWord.Length + 1 <= overlap)
                    {
                        overlapWords.Insert(0, overlapWord);
                        overlapLength += overlapWord.Length + 1;
                    }
                    else
                    {
                        break;
                    }
                }

                // Починаємо новий чанк з overlap
                currentChunk = overlapWords;
                currentLength = overlapLength;
            }

            // Додаємо слово до поточного чанку
            currentChunk.Add(word);
            currentLength += wordLength;
        }

        // Додаємо останній чанк, якщо є
        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(" ", currentChunk));
        }

        return chunks;
    }
}