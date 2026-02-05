namespace DocumentVerification.API.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string folderPath);
    Task<byte[]> GetFileAsync(string filePath);
    Task<bool> DeleteFileAsync(string filePath);
    Task<(bool isValid, string? error)> ValidateFileAsync(IFormFile file, string[] allowedTypes, long maxSizeMB);
    string GenerateUniqueFileName(string originalFileName, string documentType);
    string GetUploadPath(Guid verificationId, string documentType);
}