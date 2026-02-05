using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DocumentVerification.API.Models.Entities;

public class AuthenticityScore
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VerificationId { get; set; }

    [Range(0, 100)]
    public int OverallScore { get; set; }

    [Required]
    [StringLength(20)]
    public string Classification { get; set; } = string.Empty; // Genuine, Suspicious, Invalid

    public int? FieldCompletenessScore { get; set; }
    public int? FormatConsistencyScore { get; set; }
    public int? ImageQualityScore { get; set; }
    public int? SecurityFeaturesScore { get; set; }
    public int? MetadataConsistencyScore { get; set; }

    public string? DetailedAnalysis { get; set; } // JSON string

    public int? ProcessingTimeMs { get; set; }

    [StringLength(20)]
    public string? ModelVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore]
    public virtual Verification Verification { get; set; } = null!;
}