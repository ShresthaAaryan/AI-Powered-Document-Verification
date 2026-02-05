using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DocumentVerification.API.Models.Entities;

public class OcrResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VerificationId { get; set; }

    public string? RawText { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(5,4)")]
    public decimal? ConfidenceScore { get; set; }

    public int? ProcessingTimeMs { get; set; }

    [StringLength(10)]
    public string? LanguageDetected { get; set; }

    [StringLength(20)]
    public string? TesseractVersion { get; set; }

    [Required]
    public string ExtractedFields { get; set; } = string.Empty; // JSON string

    public string? FieldValidations { get; set; } // JSON string

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore]
    public virtual Verification Verification { get; set; } = null!;
}