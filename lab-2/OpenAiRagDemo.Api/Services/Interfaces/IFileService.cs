namespace OpenAiRagDemo.Api.Services.Interfaces;

public interface IFileService
{
    /// <summary>
    /// Зберігає файл на диск
    /// </summary>
    Task<string> SaveFileAsync(IFormFile file, string? subdirectory = null);
    
    /// <summary>
    /// Видаляє файл з диску
    /// </summary>
    Task<bool> DeleteFileAsync(string filePath);
    
    /// <summary>
    /// Перевіряє, чи існує файл
    /// </summary>
    bool FileExists(string filePath);
    
    /// <summary>
    /// Отримує повний шлях до файлу
    /// </summary>
    string GetFullPath(string fileName, string? subdirectory = null);
    
    /// <summary>
    /// Отримує Stream файлу
    /// </summary>
    Task<Stream?> GetFileStreamAsync(string filePath);
    
    /// <summary>
    /// Отримує розмір файлу в байтах
    /// </summary>
    long GetFileSize(string filePath);
}