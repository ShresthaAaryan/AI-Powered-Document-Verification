using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DocumentVerification.API.Models.Entities;

public class FaceMatchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VerificationId { get; set; }

    [Required]
    public bool IdFaceDetected { get; set; }

    [Required]
    public bool SelfieFaceDetected { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(5,4)")]
    public decimal? SimilarityScore { get; set; }

    public bool? MatchDecision { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(5,4)")]
    public decimal? ConfidenceThreshold { get; set; }

    public float[]? IdFaceEmbedding { get; set; } // Vector(512)
    public float[]? SelfieFaceEmbedding { get; set; } // Vector(512)

    public string? FaceDetectionDetails { get; set; } // JSON string

    public int? ProcessingTimeMs { get; set; }

    [StringLength(20)]
    public string? ModelVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore]
    public virtual Verification Verification { get; set; } = null!;
}