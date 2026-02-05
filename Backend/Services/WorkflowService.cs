using DocumentVerification.API.Data;
using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowStatsDto = DocumentVerification.API.Models.DTOs.Verification.WorkflowStatsDto;

namespace DocumentVerification.API.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly DocumentVerificationDbContext _context;
    private readonly IOcrService _ocrService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IFaceMatchingService _faceMatchingService;
    private readonly IDocumentService _documentService;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IServiceScopeFactory serviceScopeFactory,
        DocumentVerificationDbContext context,
        IOcrService ocrService,
        IAIAnalysisService aiAnalysisService,
        IFaceMatchingService faceMatchingService,
        IDocumentService documentService,
        ILogger<WorkflowService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _context = context;
        _ocrService = ocrService;
        _aiAnalysisService = aiAnalysisService;
        _faceMatchingService = faceMatchingService;
        _documentService = documentService;
        _logger = logger;
    }

    public async Task<VerificationDto> ProcessVerificationAsync(Guid verificationId)
    {
        try
        {
            _logger.LogInformation("Starting verification workflow for: {VerificationId}", verificationId);

            var verification = await _context.Verifications
                .Include(v => v.Documents)
                .FirstOrDefaultAsync(v => v.Id == verificationId);

            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            // Update status to Processing
            await UpdateStageAsync(verificationId, "Workflow", "Processing");
            verification.ProcessingStartedAt = DateTime.UtcNow;

            // Stage 1: OCR Processing
            // Citizenship: use back (English) for data extraction when available; otherwise front. Face matching always uses front (photo).
            await UpdateStageAsync(verificationId, "OCR", "Processing");
            var idDocument = verification.Documents.FirstOrDefault(d => d.DocumentType == "IDDocument"); // front (photo)
            var idDocumentBack = verification.Documents.FirstOrDefault(d => d.DocumentType == "IDDocumentBack");
            var ocrSourcePath = idDocument?.FilePath;
            if (ocrSourcePath != null)
            {
                var isCitizenship = string.Equals(verification.DocumentType, "CitizenshipCard", StringComparison.OrdinalIgnoreCase);
                if (isCitizenship && idDocumentBack != null)
                {
                    ocrSourcePath = idDocumentBack.FilePath; // use back for data extraction (English)
                }
                await _ocrService.ProcessVerificationAsync(verificationId, ocrSourcePath);
            }
            await UpdateStageAsync(verificationId, "OCR", "Completed");

            // Stage 2 & 3: AI Analysis and Face Matching run in parallel, each in its own scope
            // so each has its own DbContext (DbContext is not thread-safe for concurrent use).
            await UpdateStageAsync(verificationId, "AIAnalysis", "Processing");
            var ocrResult = await _context.OcrResults.FirstOrDefaultAsync(o => o.VerificationId == verificationId);
            var selfieDocument = verification.Documents.FirstOrDefault(d => d.DocumentType == "Selfie");

            var aiAnalysisTask = Task.CompletedTask;
            if (ocrResult != null && idDocument != null)
            {
                var extractedFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ExtractedFieldDto>>(ocrResult.ExtractedFields) ?? new Dictionary<string, ExtractedFieldDto>();
                var idPath = idDocument.FilePath;
                var rawText = ocrResult.RawText;
                aiAnalysisTask = RunInNewScopeAsync(async sp =>
                {
                    var ai = sp.GetRequiredService<IAIAnalysisService>();
                    await ai.AnalyzeDocumentAsync(verificationId, idPath, extractedFields, rawText);
                });
            }

            await UpdateStageAsync(verificationId, "FaceMatching", "Processing");
            var faceMatchingTask = Task.CompletedTask;
            if (idDocument != null && selfieDocument != null)
            {
                var idPath = idDocument.FilePath;
                var selfiePath = selfieDocument.FilePath;
                faceMatchingTask = RunInNewScopeAsync(async sp =>
                {
                    var face = sp.GetRequiredService<IFaceMatchingService>();
                    await face.CompareFacesAsync(verificationId, idPath, selfiePath);
                });
            }

            await Task.WhenAll(aiAnalysisTask, faceMatchingTask);
            
            await UpdateStageAsync(verificationId, "AIAnalysis", "Completed");
            await UpdateStageAsync(verificationId, "FaceMatching", "Completed");

            // Stage 4: Final Decision
            var finalResult = await MakeFinalDecisionAsync(verificationId);

            _logger.LogInformation("Verification workflow completed for: {VerificationId}, Status: {Status}",
                verificationId, finalResult.Status);

            return finalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing verification workflow for: {VerificationId}", verificationId);
            
            // Update verification with error details
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification != null)
            {
            verification.Status = "Pending"; // Reset to Pending so user can retry
            verification.ErrorMessage = GetErrorMessage(ex);
            verification.UserActionRequired = GetUserActionRequired(ex);
            verification.ProcessingStartedAt = null; // Clear processing start time
            verification.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            
            await UpdateStageAsync(verificationId, "Workflow", "Error");
            await LogWorkflowActionAsync(verificationId, "WorkflowError", "WorkflowService", "Processing", "Pending", ex.Message);
            
            // Don't throw - return the verification with error info
            return await _documentService.GetVerificationByIdAsync(verificationId) ?? throw new InvalidOperationException("Failed to retrieve verification");
        }
    }

    public async Task<VerificationDto> StartVerificationAsync(Guid verificationId)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            verification.Status = "Processing";
            verification.ProcessingStartedAt = DateTime.UtcNow;
            verification.ErrorMessage = null; // Clear any previous errors
            verification.UserActionRequired = null; // Clear any previous action required
            verification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await LogWorkflowActionAsync(verificationId, "VerificationStarted", "WorkflowService", null, "Processing");

            // Start processing in background (in production, use a proper queue system)
            // Create a new scope for the background task to avoid DbContext disposal issues
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
                try
                {
                    await workflowService.ProcessVerificationAsync(verificationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background verification processing for: {VerificationId}", verificationId);
                    // Error handling is done in ProcessVerificationAsync
                }
            });

            return await _documentService.GetVerificationByIdAsync(verificationId) ?? throw new InvalidOperationException("Failed to retrieve verification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting verification for: {VerificationId}", verificationId);
            throw;
        }
    }

    public async Task<VerificationDto> UpdateStageAsync(Guid verificationId, string stage, string status)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            var previousStatus = verification.Status;

            switch (stage.ToLowerInvariant())
            {
                case "ocr":
                    verification.Status = status == "Completed" ? "Processing" : status;
                    break;
                case "aianalysis":
                    verification.Status = status == "Completed" ? "Processing" : status;
                    break;
                case "facematching":
                    verification.Status = status == "Completed" ? "Processing" : status;
                    break;
                case "workflow":
                    verification.Status = status;
                    break;
            }

            verification.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogWorkflowActionAsync(verificationId, $"{stage}Updated", "WorkflowService", previousStatus, verification.Status);

            return await _documentService.GetVerificationByIdAsync(verificationId) ?? throw new InvalidOperationException("Failed to retrieve verification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stage for verification: {VerificationId}, Stage: {Stage}", verificationId, stage);
            throw;
        }
    }

    public async Task<VerificationDto> MakeFinalDecisionAsync(Guid verificationId)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            // Get analysis results
            var authenticityScore = await _context.AuthenticityScores.FirstOrDefaultAsync(a => a.VerificationId == verificationId);
            var faceMatchResult = await _context.FaceMatchResults.FirstOrDefaultAsync(f => f.VerificationId == verificationId);
            var ocrResult = await _context.OcrResults.FirstOrDefaultAsync(o => o.VerificationId == verificationId);

            // Decision logic
            var decision = CalculateFinalDecision(authenticityScore, faceMatchResult, ocrResult, verification.DocumentType);
            var inferredType = InferDocumentTypeFromText(ocrResult?.RawText ?? string.Empty);
            var reason = GenerateDecisionReason(decision, authenticityScore, faceMatchResult, ocrResult, verification.DocumentType, inferredType);

            verification.Status = decision;
            verification.FinalDecision = decision;
            verification.DecisionReason = reason;
            verification.CompletedAt = DateTime.UtcNow;
            verification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await LogWorkflowActionAsync(verificationId, "FinalDecision", "WorkflowService", "Processing", decision);

            _logger.LogInformation("Final decision made for verification: {VerificationId}, Decision: {Decision}, Reason: {Reason}",
                verificationId, decision, reason);

            return await _documentService.GetVerificationByIdAsync(verificationId) ?? throw new InvalidOperationException("Failed to retrieve verification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making final decision for verification: {VerificationId}", verificationId);
            throw;
        }
    }

    public async Task<bool> NeedsManualReviewAsync(Guid verificationId)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                return false;
            }

            // Get analysis results
            var authenticityScore = await _context.AuthenticityScores.FirstOrDefaultAsync(a => a.VerificationId == verificationId);
            var faceMatchResult = await _context.FaceMatchResults.FirstOrDefaultAsync(f => f.VerificationId == verificationId);

            // Check if manual review is needed
            if (authenticityScore?.Classification == "Suspicious")
            {
                return true;
            }

            if (faceMatchResult?.SimilarityScore < 0.6m && faceMatchResult.SimilarityScore > 0)
            {
                return true;
            }

            if (authenticityScore?.OverallScore < 80 && authenticityScore.OverallScore >= 50)
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if verification needs manual review: {VerificationId}", verificationId);
            return true; // Default to manual review on error
        }
    }

    public async Task<VerificationDto> AssignToOfficerAsync(Guid verificationId, string officerId)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            verification.AssignedTo = officerId;
            verification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await LogWorkflowActionAsync(verificationId, "AssignedToOfficer", "WorkflowService", null, verification.Status);

            return await _documentService.GetVerificationByIdAsync(verificationId) ?? throw new InvalidOperationException("Failed to retrieve verification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning verification to officer: {VerificationId}, Officer: {OfficerId}", verificationId, officerId);
            throw;
        }
    }

    private string CalculateFinalDecision(
        AuthenticityScore? authenticityScore,
        FaceMatchResult? faceMatchResult,
        OcrResult? ocrResult,
        string documentType)
    {
        var scores = new Dictionary<string, int>();

        // Authenticity score weighting (40%)
        if (authenticityScore != null)
        {
            scores["authenticity"] = authenticityScore.OverallScore;
        }
        else
        {
            scores["authenticity"] = 0;
        }

        // Face match weighting (30%)
        if (faceMatchResult?.MatchDecision == true)
        {
            var similarity = faceMatchResult.SimilarityScore ?? 0m;
            scores["faceMatch"] = (int)(similarity * 100);
        }
        else if (faceMatchResult?.MatchDecision == false)
        {
            scores["faceMatch"] = 20; // Low score for no match
        }
        else
        {
            scores["faceMatch"] = 50; // Medium score for inconclusive
        }

        // OCR quality weighting (30%)
        if (ocrResult != null && ocrResult.ConfidenceScore.HasValue)
        {
            scores["ocr"] = (int)(ocrResult.ConfidenceScore.Value * 100);
        }
        else
        {
            scores["ocr"] = 0;
        }

        // Document type consistency - ensure uploaded document matches selected type
        var inferredType = InferDocumentTypeFromText(ocrResult?.RawText ?? string.Empty);
        var docTypeScore = 100;
        if (!string.IsNullOrWhiteSpace(inferredType))
        {
            if (!string.Equals(inferredType, documentType, StringComparison.OrdinalIgnoreCase))
            {
                // Strong penalty if OCR text clearly looks like a different document type
                docTypeScore = 20;
                _logger.LogInformation("Document type mismatch detected. Expected: {Expected}, Inferred: {Inferred}",
                    documentType, inferredType);
            }
        }
        else
        {
            // Neutral if we couldn't infer type reliably
            docTypeScore = 70;
        }

        // Calculate weighted average (including document-type consistency)
        var finalScore = (scores["authenticity"] * 0.35) +
                         (scores["faceMatch"] * 0.25) +
                         (scores["ocr"] * 0.25) +
                         (docTypeScore * 0.15);

        return finalScore switch
        {
            >= 80 => "Approved",
            >= 60 => "ReviewNeeded",
            _ => "Rejected"
        };
    }

    private string GenerateDecisionReason(
        string decision,
        AuthenticityScore? authenticityScore,
        FaceMatchResult? faceMatchResult,
        OcrResult? ocrResult,
        string expectedDocumentType,
        string? inferredDocumentType)
    {
        var reasons = new List<string>();

        var hasDocTypeMismatch = !string.IsNullOrWhiteSpace(inferredDocumentType) &&
                                 !string.Equals(expectedDocumentType, inferredDocumentType, StringComparison.OrdinalIgnoreCase);

        switch (decision)
        {
            case "Approved":
                reasons.Add("Document passed all automated verification checks");
                if (authenticityScore?.OverallScore >= 80)
                    reasons.Add($"High authenticity score ({authenticityScore.OverallScore}/100)");
                if (faceMatchResult?.MatchDecision == true)
                {
                    var faceSimilarity = faceMatchResult.SimilarityScore ?? 0m;
                    reasons.Add($"Face verified with {(decimal)faceSimilarity * 100}% confidence");
                }
                if (ocrResult?.ConfidenceScore >= 0.8m)
                    reasons.Add($"High-quality OCR extraction ({ocrResult.ConfidenceScore:P1})");
                if (hasDocTypeMismatch)
                    reasons.Add($"Warning: Text on the document looks like {inferredDocumentType}, but {expectedDocumentType} was selected.");
                break;

            case "ReviewNeeded":
                if (hasDocTypeMismatch)
                    reasons.Add($"Selected document type ({expectedDocumentType}) does not match detected type ({inferredDocumentType}).");
                if (authenticityScore?.Classification == "Suspicious")
                    reasons.Add("Document shows suspicious characteristics requiring manual review");
                if (faceMatchResult?.SimilarityScore is { } similarity && similarity < 0.6m && similarity > 0)
                    reasons.Add("Face match similarity below threshold for automatic approval");
                if (authenticityScore?.OverallScore < 80 && authenticityScore.OverallScore >= 50)
                    reasons.Add("Moderate authenticity score requires verification");
                reasons.Add("Manual review recommended to verify document authenticity");
                break;

            case "Rejected":
                if (hasDocTypeMismatch)
                    reasons.Add($"Uploaded document appears to be {inferredDocumentType}, but {expectedDocumentType} was selected.");
                if (authenticityScore?.Classification == "Invalid")
                    reasons.Add("Document classified as invalid");
                if (faceMatchResult?.MatchDecision == false)
                    reasons.Add("Face verification failed");
                if (authenticityScore?.OverallScore < 50)
                    reasons.Add($"Low authenticity score ({authenticityScore.OverallScore}/100)");
                if (ocrResult?.ConfidenceScore < 0.5m)
                    reasons.Add("Poor OCR quality indicates potential issues");
                reasons.Add("Document did not meet minimum verification standards");
                break;
        }

        return string.Join("; ", reasons);
    }

    public async Task<WorkflowStatsDto> GetWorkflowStatsAsync(string? userId, bool isAdmin)
    {
        try
        {
            var query = _context.Verifications.AsQueryable();

            // Filter by user if not admin
            if (!isAdmin && !string.IsNullOrEmpty(userId))
            {
                query = query.Where(v => v.UserId == userId);
            }

            var today = DateTime.UtcNow.Date;
            var todayEnd = today.AddDays(1);

            var totalVerifications = await query.CountAsync();
            var pendingVerifications = await query.CountAsync(v => v.Status == "Pending");
            var processingVerifications = await query.CountAsync(v => v.Status == "Processing");
            var completedVerifications = await query.CountAsync(v => v.Status == "Approved" || v.Status == "Rejected");
            var approvedVerifications = await query.CountAsync(v => v.Status == "Approved");
            var rejectedVerifications = await query.CountAsync(v => v.Status == "Rejected");
            var reviewNeededVerifications = await query.CountAsync(v => v.Status == "ReviewNeeded");
            var todayVerifications = await query.CountAsync(v => v.CreatedAt >= today && v.CreatedAt < todayEnd);

            // Calculate average processing time
            var completedWithTimes = await query
                .Where(v => (v.Status == "Approved" || v.Status == "Rejected") && 
                           v.ProcessingStartedAt != null && 
                           v.CompletedAt != null)
                .Select(v => new { v.ProcessingStartedAt, v.CompletedAt })
                .ToListAsync();

            var averageProcessingTimeMinutes = 0.0;
            if (completedWithTimes.Any())
            {
                var totalMinutes = completedWithTimes
                    .Where(v => v.ProcessingStartedAt.HasValue && v.CompletedAt.HasValue)
                    .Sum(v => (v.CompletedAt!.Value - v.ProcessingStartedAt!.Value).TotalMinutes);
                averageProcessingTimeMinutes = totalMinutes / completedWithTimes.Count;
            }

            return new WorkflowStatsDto
            {
                totalVerifications = totalVerifications,
                pendingVerifications = pendingVerifications,
                processingVerifications = processingVerifications,
                completedVerifications = completedVerifications,
                approvedVerifications = approvedVerifications,
                rejectedVerifications = rejectedVerifications,
                reviewNeededVerifications = reviewNeededVerifications,
                averageProcessingTimeMinutes = Math.Round(averageProcessingTimeMinutes, 2),
                todayVerifications = todayVerifications
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow stats for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<int> ResetProcessingVerificationsAsync()
    {
        try
        {
            var processingVerifications = await _context.Verifications
                .Where(v => v.Status == "Processing")
                .ToListAsync();

            var count = processingVerifications.Count;

            foreach (var verification in processingVerifications)
            {
                verification.Status = "Pending";
                verification.ProcessingStartedAt = null;
                verification.UpdatedAt = DateTime.UtcNow;

                await LogWorkflowActionAsync(verification.Id, "VerificationReset", "WorkflowService", "Processing", "Pending");
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Reset {Count} verification(s) from Processing to Pending", count);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting processing verifications");
            throw;
        }
    }

    private string? InferDocumentTypeFromText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        var text = rawText.ToUpperInvariant();

        // Basic heuristics based on prominent labels/keywords on Nepali documents
        if (text.Contains("PASSPORT") || text.Contains("P<") || text.Contains("MRP"))
        {
            return "Passport";
        }

        if (text.Contains("DRIVING LICENSE") || text.Contains("DRIVER'S LICENSE") ||
            text.Contains("DRIVER LICENCE") || text.Contains("D.L.NO") || text.Contains("DRIVING LICENCE"))
        {
            return "DriversLicense";
        }

        if (text.Contains("NATIONAL IDENTITY") || text.Contains("NATIONAL IDENTITY CARD") ||
            text.Contains("NATIONAL IDENTITY NUMBER") || text.Contains("NIN"))
        {
            return "NationalID";
        }

        if (text.Contains("CITIZENSHIP") || text.Contains("नागरिकता"))
        {
            return "CitizenshipCard";
        }

        return null;
    }

    private string GetErrorMessage(Exception ex)
    {
        return ex switch
        {
            InvalidOperationException => ex.Message,
            FileNotFoundException => "Required document files were not found. Please ensure all files were uploaded correctly.",
            UnauthorizedAccessException => "Access denied to required resources. Please contact support.",
            TimeoutException => "Processing timed out. The verification took too long to complete.",
            _ => $"An error occurred during processing: {ex.Message}"
        };
    }

    private string GetUserActionRequired(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => "Please re-upload your documents and try again. Ensure the files are in supported formats (JPG, PNG, PDF).",
            InvalidOperationException when ex.Message.Contains("OCR") => "The document image quality may be too low. Please upload a clearer, higher resolution image and try again.",
            InvalidOperationException when ex.Message.Contains("Face") => "Face detection failed. Please ensure your face is clearly visible in both the ID document and selfie, with good lighting.",
            TimeoutException => "The verification process is taking longer than expected. Please wait a few minutes and try refreshing, or contact support if the issue persists.",
            _ => "Please try starting the verification process again. If the problem continues, contact support with your reference number."
        };
    }

    /// <summary>Runs an async delegate in a new DI scope with its own DbContext. Use to avoid concurrent operations on the same DbContext.</summary>
    private async Task RunInNewScopeAsync(Func<IServiceProvider, Task> run)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        await run(scope.ServiceProvider);
    }

    private async Task LogWorkflowActionAsync(Guid verificationId, string action, string serviceName, string? previousStatus, string? newStatus, string? errorMessage = null)
    {
        var log = new VerificationLog
        {
            Id = Guid.NewGuid(),
            VerificationId = verificationId,
            Action = action,
            ServiceName = serviceName,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        _context.VerificationLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}