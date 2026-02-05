using DocumentVerification.API.Models.DTOs.Verification;
using WorkflowStatsDto = DocumentVerification.API.Models.DTOs.Verification.WorkflowStatsDto;

namespace DocumentVerification.API.Services;

public interface IWorkflowService
{
    Task<VerificationDto> ProcessVerificationAsync(Guid verificationId);
    Task<VerificationDto> StartVerificationAsync(Guid verificationId);
    Task<VerificationDto> UpdateStageAsync(Guid verificationId, string stage, string status);
    Task<VerificationDto> MakeFinalDecisionAsync(Guid verificationId);
    Task<bool> NeedsManualReviewAsync(Guid verificationId);
    Task<VerificationDto> AssignToOfficerAsync(Guid verificationId, string officerId);
    Task<WorkflowStatsDto> GetWorkflowStatsAsync(string? userId, bool isAdmin);
    Task<int> ResetProcessingVerificationsAsync();
}