using System.Text;
using Docnet.Core;
using Docnet.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Services;

public class PdfService : IPdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Витягує текст з PDF
    /// </summary>
    public string ExtractText(byte[] pdfBytes, int maxPages = 10)
    {
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions());
            int totalPages = docReader.GetPageCount();
            int pagesToExtract = Math.Min(totalPages, maxPages);

            var text = new StringBuilder();
            
            for (int i = 0; i < pagesToExtract; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                text.AppendLine(pageReader.GetText());
                text.AppendLine();
            }

            _logger.LogInformation("Extracted text from {Pages} pages", pagesToExtract);
            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            return string.Empty;
        }
    }

    /// <summary>
    /// Конвертує всі сторінки PDF в PNG bytes
    /// </summary>
    public async Task<List<byte[]>> ConvertPdfToPngBytesAsync(
        byte[] pdfBytes, 
        int maxPages = 10, 
        int dpi = 200)
    {
        var pngBytesList = new List<byte[]>();

        try
        {
            // Створюємо dimensions один раз
            var dimensions = new PageDimensions(
                (int)(8.27 * dpi),   // A4 width in pixels
                (int)(11.69 * dpi)   // A4 height in pixels
            );
            
            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, dimensions);
            
            int totalPages = docReader.GetPageCount();
            int pagesToRender = Math.Min(totalPages, maxPages);

            _logger.LogInformation(
                "Converting {Pages} of {Total} pages to PNG at {DPI} DPI", 
                pagesToRender, totalPages, dpi);

            for (int i = 0; i < pagesToRender; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                
                var rawBytes = pageReader.GetImage();
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();

                // Використовуємо ImageSharp для конвертації BGRA32 -> PNG
                using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
                
                using var ms = new MemoryStream();
                await image.SaveAsync(ms, new PngEncoder());
                pngBytesList.Add(ms.ToArray());

                _logger.LogDebug("Converted page {Page}/{Total} ({Width}x{Height})", 
                    i + 1, pagesToRender, width, height);
            }

            _logger.LogInformation("Successfully converted {Count} pages to PNG", pngBytesList.Count);
            return pngBytesList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting PDF to PNG bytes");
            throw;
        }
    }

    /// <summary>
    /// Отримує кількість сторінок у PDF
    /// </summary>
    public int GetPageCount(byte[] pdfBytes)
    {
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions());
            return docReader.GetPageCount();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting page count");
            return 0;
        }
    }

    /// <summary>
    /// Читає Stream у byte array
    /// </summary>
    public byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Async версія ReadAllBytes
    /// </summary>
    public async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        if (stream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}