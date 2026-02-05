namespace DocumentVerification.API.Models.DTOs.Verification;

public class VerificationDto
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? FinalDecision { get; set; }
    public string? DecisionReason { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserActionRequired { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SubmittedAt { get; set; }

    // Related data
    public OcrResultDto? OcrResult { get; set; }
    public AuthenticityScoreDto? AuthenticityScore { get; set; }
    public FaceMatchResultDto? FaceMatchResult { get; set; }
    public List<DocumentDto> Documents { get; set; } = new();

    // User information
    public string? SubmittedBy { get; set; }
    public string? AssignedTo { get; set; }
}

public class OcrResultDto
{
    public Guid Id { get; set; }
    public string? RawText { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public string? LanguageDetected { get; set; }
    public Dictionary<string, ExtractedFieldDto>? ExtractedFields { get; set; }
}

public class ExtractedFieldDto
{
    public string? Value { get; set; }
    public decimal Confidence { get; set; }
    public BoundingBoxDto? BoundingBox { get; set; }
    public string? Format { get; set; }
}

public class BoundingBoxDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class AuthenticityScoreDto
{
    public Guid Id { get; set; }
    public int OverallScore { get; set; }
    public string Classification { get; set; } = string.Empty;
    public int? FieldCompletenessScore { get; set; }
    public int? FormatConsistencyScore { get; set; }
    public int? ImageQualityScore { get; set; }
    public int? SecurityFeaturesScore { get; set; }
    public int? MetadataConsistencyScore { get; set; }
    public object? DetailedAnalysis { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public string? ModelVersion { get; set; }
}

public class FaceMatchResultDto
{
    public Guid Id { get; set; }
    public bool IdFaceDetected { get; set; }
    public bool SelfieFaceDetected { get; set; }
    public decimal? SimilarityScore { get; set; }
    public bool? MatchDecision { get; set; }
    public decimal? ConfidenceThreshold { get; set; }
    public object? FaceDetectionDetails { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public string? ModelVersion { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool IsPrimary { get; set; }
}