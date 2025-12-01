namespace OpenAiRagDemo.Api.Services.Interfaces;

public interface IPdfService
{
    /// <summary>
    /// Витягує текст з PDF
    /// </summary>
    string ExtractText(byte[] pdfBytes, int maxPages = 10);

    /// <summary>
    /// Конвертує всі сторінки PDF в PNG bytes
    /// </summary>
    Task<List<byte[]>> ConvertPdfToPngBytesAsync(byte[] pdfBytes, int maxPages = 10, int dpi = 200);

    /// <summary>
    /// Отримує кількість сторінок у PDF
    /// </summary>
    int GetPageCount(byte[] pdfBytes);

    /// <summary>
    /// Читає Stream у byte array
    /// </summary>
    byte[] ReadAllBytes(Stream stream);

    /// <summary>
    /// Async версія ReadAllBytes
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(Stream stream);
}