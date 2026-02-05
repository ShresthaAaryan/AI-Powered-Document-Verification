using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DocumentVerification.API.Models.Entities;

public class Verification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(20)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public string? UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    [StringLength(10)]
    public string Priority { get; set; } = "Normal";

    public string? SubmittedBy { get; set; }
    public string? AssignedTo { get; set; }

    [StringLength(20)]
    public string? FinalDecision { get; set; }

    [StringLength(1000)]
    public string? DecisionReason { get; set; }

    [StringLength(2000)]
    public string? ErrorMessage { get; set; }

    [StringLength(1000)]
    public string? UserActionRequired { get; set; }

    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    [JsonIgnore]
    public virtual ICollection<VerificationLog> Logs { get; set; } = new List<VerificationLog>();
    [JsonIgnore]
    public virtual ICollection<OcrResult> OcrResults { get; set; } = new List<OcrResult>();
    [JsonIgnore]
    public virtual ICollection<FaceMatchResult> FaceMatchResults { get; set; } = new List<FaceMatchResult>();
    [JsonIgnore]
    public virtual ICollection<AuthenticityScore> AuthenticityScores { get; set; } = new List<AuthenticityScore>();
}