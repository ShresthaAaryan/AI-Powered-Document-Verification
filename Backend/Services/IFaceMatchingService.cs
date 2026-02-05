using DocumentVerification.API.Models.DTOs.Verification;

namespace DocumentVerification.API.Services;

public interface IFaceMatchingService
{
    Task<FaceMatchResultDto> CompareFacesAsync(Guid verificationId, string idDocumentPath, string selfiePath);
    Task<float[]?> GenerateFaceEmbeddingAsync(string imagePath);
    Task<float> CalculateSimilarityScore(float[] embedding1, float[] embedding2);
    Task<bool> DetectFaceAsync(string imagePath);
    Task<FaceDetectionDetails> DetectFaceDetailsAsync(string imagePath);
}