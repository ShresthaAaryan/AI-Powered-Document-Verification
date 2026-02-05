using DocumentVerification.API.Data;
using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocumentVerification.API.Services;

public class DocumentService : IDocumentService
{
    private readonly DocumentVerificationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<DocumentService> _logger;
    private readonly IOcrService _ocrService;

    public DocumentService(
        DocumentVerificationDbContext context,
        IFileStorageService fileStorageService,
        UserManager<IdentityUser> userManager,
        ILogger<DocumentService> logger,
        IOcrService ocrService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _userManager = userManager;
        _logger = logger;
        _ocrService = ocrService;
    }

    public async Task<VerificationDto> CreateVerificationAsync(CreateVerificationRequest request, string userId)
    {
        try
        {
            // Generate unique reference number
            var referenceNumber = await GenerateReferenceNumberAsync();

            var verification = new Verification
            {
                Id = Guid.NewGuid(),
                ReferenceNumber = referenceNumber,
                UserId = userId,
                DocumentType = request.DocumentType,
                Status = "Pending",
                Priority = request.Priority,
                SubmittedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save verification first
            _context.Verifications.Add(verification);
            await _context.SaveChangesAsync();

            // Process ID document
            var idValidation = await _fileStorageService.ValidateFileAsync(
                request.IdDocument,
                new[] { ".jpg", ".jpeg", ".png", ".pdf", ".tiff" },
                10);

            if (!idValidation.isValid)
            {
                throw new InvalidOperationException($"ID document validation failed: {idValidation.error}");
            }

            var idUploadPath = _fileStorageService.GetUploadPath(verification.Id, "IDDocument");
            var idFilePath = await _fileStorageService.SaveFileAsync(request.IdDocument, idUploadPath);

            // Validate that the uploaded document matches the claimed document type
            try
            {
                var detectedType = await _ocrService.DetectDocumentTypeAsync(idFilePath);
                if (detectedType != null)
                {
                    var claimedType = NormalizeDocumentType(request.DocumentType);
                    var normalizedDetectedType = NormalizeDocumentType(detectedType);
                    
                    if (normalizedDetectedType != claimedType)
                    {
                        _logger.LogWarning(
                            "Document type mismatch detected. Claimed: {ClaimedType}, Detected: {DetectedType} for verification: {VerificationId}",
                            claimedType, normalizedDetectedType, verification.Id);
                        
                        // Clean up saved file
                        try
                        {
                            await _fileStorageService.DeleteFileAsync(idFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete file after type mismatch: {FilePath}", idFilePath);
                        }
                        
                        // Remove verification record
                        _context.Verifications.Remove(verification);
                        await _context.SaveChangesAsync();
                        
                        throw new InvalidOperationException(
                            $"The uploaded document does not match the selected document type. " +
                            $"You selected '{request.DocumentType}', but the document appears to be a '{detectedType}'. " +
                            $"Please upload the correct document type.");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Document type validation passed. Claimed: {ClaimedType}, Detected: {DetectedType} for verification: {VerificationId}",
                            claimedType, normalizedDetectedType, verification.Id);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Could not detect document type from uploaded file. Proceeding with claimed type: {ClaimedType} for verification: {VerificationId}",
                        request.DocumentType, verification.Id);
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw validation errors
                throw;
            }
            catch (Exception ex)
            {
                // Log but don't fail the upload if document type detection fails
                // This allows the system to continue processing even if OCR is temporarily unavailable
                _logger.LogWarning(ex, 
                    "Document type detection failed for verification: {VerificationId}. Proceeding with claimed type: {ClaimedType}",
                    verification.Id, request.DocumentType);
            }

            var idDocument = new Document
            {
                Id = Guid.NewGuid(),
                VerificationId = verification.Id,
                DocumentType = "IDDocument",
                FileName = Path.GetFileName(idFilePath),
                FilePath = idFilePath,
                FileSizeBytes = request.IdDocument.Length,
                MimeType = request.IdDocument.ContentType,
                OriginalFileName = request.IdDocument.FileName,
                IsPrimary = true,
                UploadedAt = DateTime.UtcNow
            };

            // Process selfie
            var selfieValidation = await _fileStorageService.ValidateFileAsync(
                request.SelfieImage,
                new[] { ".jpg", ".jpeg", ".png", ".bmp" },
                5);

            if (!selfieValidation.isValid)
            {
                throw new InvalidOperationException($"Selfie validation failed: {selfieValidation.error}");
            }

            var selfieUploadPath = _fileStorageService.GetUploadPath(verification.Id, "Selfie");
            var selfieFilePath = await _fileStorageService.SaveFileAsync(request.SelfieImage, selfieUploadPath);

            var selfieDocument = new Document
            {
                Id = Guid.NewGuid(),
                VerificationId = verification.Id,
                DocumentType = "Selfie",
                FileName = Path.GetFileName(selfieFilePath),
                FilePath = selfieFilePath,
                FileSizeBytes = request.SelfieImage.Length,
                MimeType = request.SelfieImage.ContentType,
                OriginalFileName = request.SelfieImage.FileName,
                IsPrimary = false,
                UploadedAt = DateTime.UtcNow
            };

            var documentsToAdd = new List<Document> { idDocument, selfieDocument };

            // CitizenshipCard only: optional back side (data in English)
            var isCitizenship = NormalizeDocumentType(request.DocumentType) == "citizenshipcard";
            if (isCitizenship && request.IdDocumentBack != null)
            {
                var backValidation = await _fileStorageService.ValidateFileAsync(
                    request.IdDocumentBack,
                    new[] { ".jpg", ".jpeg", ".png", ".pdf", ".tiff" },
                    10);
                if (backValidation.isValid)
                {
                    var backUploadPath = _fileStorageService.GetUploadPath(verification.Id, "IDDocumentBack");
                    var backFilePath = await _fileStorageService.SaveFileAsync(request.IdDocumentBack, backUploadPath);
                    var backDocument = new Document
                    {
                        Id = Guid.NewGuid(),
                        VerificationId = verification.Id,
                        DocumentType = "IDDocumentBack",
                        FileName = Path.GetFileName(backFilePath),
                        FilePath = backFilePath,
                        FileSizeBytes = request.IdDocumentBack.Length,
                        MimeType = request.IdDocumentBack.ContentType,
                        OriginalFileName = request.IdDocumentBack.FileName,
                        IsPrimary = false,
                        UploadedAt = DateTime.UtcNow
                    };
                    documentsToAdd.Add(backDocument);
                    _logger.LogInformation("Citizenship back side saved for verification: {VerificationId}", verification.Id);
                }
                else
                {
                    _logger.LogWarning("Citizenship back side validation failed: {Error}", backValidation.error);
                }
            }

            _context.Documents.AddRange(documentsToAdd);
            await _context.SaveChangesAsync();

            // Log the creation
            await LogVerificationActionAsync(verification.Id, userId, "VerificationStarted", "DocumentService", null, "Pending");

            return await MapToVerificationDtoAsync(verification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating verification for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<VerificationDto?> GetVerificationByIdAsync(Guid id)
    {
        try
        {
            var verification = await _context.Verifications
                .Include(v => v.Documents)
                .FirstOrDefaultAsync(v => v.Id == id);

            return verification != null ? await MapToVerificationDtoAsync(verification) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verification: {VerificationId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<VerificationDto>> GetUserVerificationsAsync(string userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var verifications = await _context.Verifications
                .Include(v => v.Documents)
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = new List<VerificationDto>();
            foreach (var verification in verifications)
            {
                dtos.Add(await MapToVerificationDtoAsync(verification));
            }

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verifications for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<VerificationDto>> GetAllVerificationsAsync(int page = 1, int pageSize = 20, string? status = null)
    {
        try
        {
            var query = _context.Verifications
                .Include(v => v.Documents)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(v => v.Status == status);
            }

            var verifications = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = new List<VerificationDto>();
            foreach (var verification in verifications)
            {
                dtos.Add(await MapToVerificationDtoAsync(verification));
            }

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all verifications");
            throw;
        }
    }

    public async Task<VerificationDto> UpdateVerificationStatusAsync(Guid id, string status, string? reason = null)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(id);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            var previousStatus = verification.Status;
            verification.Status = status;
            verification.UpdatedAt = DateTime.UtcNow;

            if (status == "Approved" || status == "Rejected")
            {
                verification.FinalDecision = status;
                verification.DecisionReason = reason;
                verification.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await LogVerificationActionAsync(verification.Id, null, "StatusUpdated", "DocumentService", previousStatus, status);

            return await MapToVerificationDtoAsync(verification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating verification status: {VerificationId}", id);
            throw;
        }
    }

    public async Task<byte[]> GetDocumentFileAsync(Guid documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                throw new InvalidOperationException("Document not found");
            }

            return await _fileStorageService.GetFileAsync(document.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document file: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> DeleteVerificationAsync(Guid id)
    {
        try
        {
            var verification = await _context.Verifications
                .Include(v => v.Documents)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (verification == null)
            {
                return false;
            }

            // Delete associated files
            foreach (var document in verification.Documents)
            {
                await _fileStorageService.DeleteFileAsync(document.FilePath);
            }

            _context.Verifications.Remove(verification);
            await _context.SaveChangesAsync();

            await LogVerificationActionAsync(verification.Id, null, "VerificationDeleted", "DocumentService", null, null);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting verification: {VerificationId}", id);
            throw;
        }
    }

    private async Task<string> GenerateReferenceNumberAsync()
    {
        var prefix = "DV";
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = new Random().Next(10000, 99999);
        var referenceNumber = $"{prefix}{date}{random}";

        // Ensure uniqueness
        var exists = await _context.Verifications.AnyAsync(v => v.ReferenceNumber == referenceNumber);
        if (exists)
        {
            return await GenerateReferenceNumberAsync(); // Recursive call with new random
        }

        return referenceNumber;
    }

    /// <summary>
    /// Normalizes document type names to ensure consistent comparison
    /// </summary>
    private string NormalizeDocumentType(string documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return documentType;

        var normalized = documentType.Trim().ToLowerInvariant();
        
        if (normalized == "passport")
            return "passport";
        
        if (normalized == "driverslicense" || normalized == "driver's license" || normalized == "driving license")
            return "driverslicense";
        
        if (normalized == "nationalid" || normalized == "national id" || normalized == "national identity")
            return "nationalid";
        
        if (normalized == "citizenshipcard" || normalized == "citizenship card" || normalized == "citizenship")
            return "citizenshipcard";
        
        return normalized;
    }

    private async Task LogVerificationActionAsync(Guid verificationId, string? userId, string action, string serviceName, string? previousStatus, string? newStatus)
    {
        var log = new VerificationLog
        {
            Id = Guid.NewGuid(),
            VerificationId = verificationId,
            UserId = userId,
            Action = action,
            ServiceName = serviceName,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            CreatedAt = DateTime.UtcNow
        };

        _context.VerificationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    private async Task<VerificationDto> MapToVerificationDtoAsync(Verification verification)
    {
        var documents = await _context.Documents
            .Where(d => d.VerificationId == verification.Id)
            .ToListAsync();

        var documentDtos = documents.Select(d => new DocumentDto
        {
            Id = d.Id,
            DocumentType = d.DocumentType,
            FileName = d.FileName,
            FileSizeBytes = d.FileSizeBytes,
            MimeType = d.MimeType,
            OriginalFileName = d.OriginalFileName,
            UploadedAt = d.UploadedAt,
            IsPrimary = d.IsPrimary
        }).ToList();

        // Get related data if exists
        var ocrResult = await _context.OcrResults.FirstOrDefaultAsync(o => o.VerificationId == verification.Id);
        var authenticityScore = await _context.AuthenticityScores.FirstOrDefaultAsync(a => a.VerificationId == verification.Id);
        var faceMatchResult = await _context.FaceMatchResults.FirstOrDefaultAsync(f => f.VerificationId == verification.Id);

        return new VerificationDto
        {
            Id = verification.Id,
            ReferenceNumber = verification.ReferenceNumber,
            DocumentType = verification.DocumentType,
            Status = verification.Status,
            Priority = verification.Priority,
            FinalDecision = verification.FinalDecision,
            DecisionReason = verification.DecisionReason,
            ErrorMessage = verification.ErrorMessage,
            UserActionRequired = verification.UserActionRequired,
            ProcessingStartedAt = verification.ProcessingStartedAt,
            CompletedAt = verification.CompletedAt,
            CreatedAt = verification.CreatedAt,
            UpdatedAt = verification.UpdatedAt,
            SubmittedAt = verification.CreatedAt,
            OcrResult = ocrResult == null ? null : new OcrResultDto
            {
                Id = ocrResult.Id,
                RawText = ocrResult.RawText,
                ConfidenceScore = ocrResult.ConfidenceScore,
                ProcessingTimeMs = ocrResult.ProcessingTimeMs,
                LanguageDetected = ocrResult.LanguageDetected,
                ExtractedFields = string.IsNullOrWhiteSpace(ocrResult.ExtractedFields)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ExtractedFieldDto>>(ocrResult.ExtractedFields)
            },
            AuthenticityScore = authenticityScore == null ? null : new AuthenticityScoreDto
            {
                Id = authenticityScore.Id,
                OverallScore = authenticityScore.OverallScore,
                Classification = authenticityScore.Classification,
                FieldCompletenessScore = authenticityScore.FieldCompletenessScore,
                FormatConsistencyScore = authenticityScore.FormatConsistencyScore,
                ImageQualityScore = authenticityScore.ImageQualityScore,
                SecurityFeaturesScore = authenticityScore.SecurityFeaturesScore,
                MetadataConsistencyScore = authenticityScore.MetadataConsistencyScore,
                DetailedAnalysis = string.IsNullOrWhiteSpace(authenticityScore.DetailedAnalysis)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<object>(authenticityScore.DetailedAnalysis),
                ProcessingTimeMs = authenticityScore.ProcessingTimeMs,
                ModelVersion = authenticityScore.ModelVersion
            },
            FaceMatchResult = faceMatchResult == null ? null : new FaceMatchResultDto
            {
                Id = faceMatchResult.Id,
                IdFaceDetected = faceMatchResult.IdFaceDetected,
                SelfieFaceDetected = faceMatchResult.SelfieFaceDetected,
                SimilarityScore = faceMatchResult.SimilarityScore,
                MatchDecision = faceMatchResult.MatchDecision,
                ConfidenceThreshold = faceMatchResult.ConfidenceThreshold,
                FaceDetectionDetails = string.IsNullOrWhiteSpace(faceMatchResult.FaceDetectionDetails)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<object>(faceMatchResult.FaceDetectionDetails),
                ProcessingTimeMs = faceMatchResult.ProcessingTimeMs,
                ModelVersion = faceMatchResult.ModelVersion
            },
            Documents = documentDtos,
            SubmittedBy = verification.SubmittedBy,
            AssignedTo = verification.AssignedTo
        };
    }
}