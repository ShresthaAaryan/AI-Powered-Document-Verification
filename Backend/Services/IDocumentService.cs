using DocumentVerification.API.Models.DTOs.Verification;

namespace DocumentVerification.API.Services;

public interface IDocumentService
{
    Task<VerificationDto> CreateVerificationAsync(CreateVerificationRequest request, string userId);
    Task<VerificationDto?> GetVerificationByIdAsync(Guid id);
    Task<IEnumerable<VerificationDto>> GetUserVerificationsAsync(string userId, int page = 1, int pageSize = 20);
    Task<IEnumerable<VerificationDto>> GetAllVerificationsAsync(int page = 1, int pageSize = 20, string? status = null);
    Task<VerificationDto> UpdateVerificationStatusAsync(Guid id, string status, string? reason = null);
    Task<byte[]> GetDocumentFileAsync(Guid documentId);
    Task<bool> DeleteVerificationAsync(Guid id);
}