using DocumentVerification.API.Models.Entities;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DocumentVerification.API.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string folderPath)
    {
        try
        {
            // Create directory if it doesn't exist
            Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, GenerateUniqueFileName(file.FileName, "upload"));

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Process image if it's an image file
            if (IsImageFile(file.FileName))
            {
                await ProcessImageAsync(filePath);
            }

            _logger.LogInformation("File saved successfully: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file: {FileName}", file.FileName);
            throw;
        }
    }

    public async Task<byte[]> GetFileAsync(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            return await System.IO.File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<(bool isValid, string? error)> ValidateFileAsync(IFormFile file, string[] allowedTypes, long maxSizeMB)
    {
        await Task.CompletedTask;

        if (file == null || file.Length == 0)
        {
            return (false, "No file selected");
        }

        // Check file size
        var maxSizeBytes = maxSizeMB * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            return (false, $"File size exceeds maximum allowed size of {maxSizeMB}MB");
        }

        // Check file extension
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedTypes.Contains(fileExtension))
        {
            return (false, $"File type {fileExtension} is not allowed. Allowed types: {string.Join(", ", allowedTypes)}");
        }

        // Additional validation for image files
        if (IsImageFile(file.FileName))
        {
            try
            {
                using var image = Image.Load(file.OpenReadStream());
                if (image.Width < 100 || image.Height < 100)
                {
                    return (false, "Image dimensions are too small (minimum 100x100 pixels)");
                }
            }
            catch
            {
                return (false, "Invalid image file");
            }
        }

        return (true, null);
    }

    public string GenerateUniqueFileName(string originalFileName, string documentType)
    {
        var extension = Path.GetExtension(originalFileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{documentType}_{timestamp}_{random}{extension}";
    }

    public string GetUploadPath(Guid verificationId, string documentType)
    {
        var basePath = _configuration["FileStorage:BasePath"] ?? "./uploads";
        var yearMonth = DateTime.UtcNow.ToString("yyyy/MM");
        var verificationPath = Path.Combine(basePath, yearMonth, verificationId.ToString());
        var documentPath = Path.Combine(verificationPath, documentType);
        return documentPath;
    }

    private async Task ProcessImageAsync(string filePath)
    {
        try
        {
            using var image = await Image.LoadAsync(filePath);

            // Resize if too large (max 2000x2000)
            if (image.Width > 2000 || image.Height > 2000)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(2000, 2000),
                    Mode = ResizeMode.Max
                }));
            }

            // Convert to JPEG for consistency
            var outputPath = Path.ChangeExtension(filePath, ".jpg");
            await image.SaveAsJpegAsync(outputPath);

            // Remove original if it was different format
            if (filePath != outputPath)
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image: {FilePath}", filePath);
            // Don't throw here, as processing failure shouldn't block upload
        }
    }

    private bool IsImageFile(string fileName)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return imageExtensions.Contains(extension);
    }
}