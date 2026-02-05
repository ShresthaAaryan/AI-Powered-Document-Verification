using DocumentVerification.API.Models.DTOs.Verification;

namespace DocumentVerification.API.Services;

public interface IAIAnalysisService
{
    Task<AuthenticityScoreDto> AnalyzeDocumentAsync(
        Guid verificationId,
        string imagePath,
        Dictionary<string, ExtractedFieldDto> extractedFields,
        string? rawText = null);
    Task<int> CalculateFieldCompletenessScore(Dictionary<string, ExtractedFieldDto> fields, string documentType);
    Task<int> CalculateFormatConsistencyScore(Dictionary<string, ExtractedFieldDto> fields, string documentType);
    Task<int> CalculateImageQualityScore(string imagePath);
    Task<int> CalculateSecurityFeaturesScore(string imagePath);
    Task<int> CalculateMetadataConsistencyScore(string imagePath);
}