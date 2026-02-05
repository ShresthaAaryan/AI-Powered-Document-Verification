using DocumentVerification.API.Models.DTOs.Verification;

namespace DocumentVerification.API.Services;

public interface IOcrService
{
    Task<OcrResultDto> ExtractTextAsync(Guid verificationId, string imagePath, string documentType);
    Task<OcrResultDto> ProcessVerificationAsync(Guid verificationId, string idDocumentPath);
    Task<Dictionary<string, ExtractedFieldDto>> ParseExtractedFieldsAsync(string rawText, string documentType);
    Task<bool> ValidateExtractedFieldsAsync(Dictionary<string, ExtractedFieldDto> fields, string documentType);
    Task<string?> DetectDocumentTypeAsync(string imagePath);
}