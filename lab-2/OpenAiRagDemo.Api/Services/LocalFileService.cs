using OpenAiRagDemo.Api.Services.Interfaces;

namespace OpenAiRagDemo.Api.Services;

public class LocalFileService : IFileService
{
    private readonly string _baseFolder;
    private readonly ILogger<LocalFileService> _logger;

    public LocalFileService(IConfiguration configuration, ILogger<LocalFileService> logger)
    {
        _logger = logger;
        _baseFolder = configuration["Storage:PdfFolder"] ?? "./uploads";
        
        // Створити базову папку, якщо не існує
        if (!Directory.Exists(_baseFolder))
        {
            Directory.CreateDirectory(_baseFolder);
            _logger.LogInformation("Created base folder: {Folder}", _baseFolder);
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, string? subdirectory = null)
    {
        try
        {
            // Визначити папку для збереження
            var targetFolder = string.IsNullOrEmpty(subdirectory) 
                ? _baseFolder 
                : Path.Combine(_baseFolder, subdirectory);

            // Створити підпапку, якщо потрібно
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            // Генерувати унікальне ім'я файлу
            var fileName = $"{Guid.NewGuid()}_{SanitizeFileName(file.FileName)}";
            var filePath = Path.Combine(targetFolder, fileName);

            // Зберегти файл
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("File saved: {FilePath}, Size: {Size} bytes", filePath, file.Length);

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file: {FileName}", file.FileName);
            throw new IOException($"Failed to save file: {ex.Message}", ex);
        }
    }

    public Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("File deleted: {FilePath}", filePath);
                return Task.FromResult(true);
            }

            _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return Task.FromResult(false);
        }
    }

    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    public string GetFullPath(string fileName, string? subdirectory = null)
    {
        var targetFolder = string.IsNullOrEmpty(subdirectory) 
            ? _baseFolder 
            : Path.Combine(_baseFolder, subdirectory);

        return Path.Combine(targetFolder, fileName);
    }

    public Task<Stream?> GetFileStreamAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return Task.FromResult<Stream?>(null);
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file stream: {FilePath}", filePath);
            return Task.FromResult<Stream?>(null);
        }
    }

    public long GetFileSize(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return 0;
            }

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file size: {FilePath}", filePath);
            return 0;
        }
    }

    /// <summary>
    /// Очищає ім'я файлу від небезпечних символів
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized;
    }
}