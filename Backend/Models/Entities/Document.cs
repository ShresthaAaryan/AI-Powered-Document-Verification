using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DocumentVerification.API.Models.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VerificationId { get; set; }

    [Required]
    [StringLength(50)]
    public string DocumentType { get; set; } = string.Empty; // IDDocument, Selfie, SupportingDocument

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public long FileSizeBytes { get; set; }

    [Required]
    [StringLength(100)]
    public string MimeType { get; set; } = string.Empty;

    [StringLength(255)]
    public string? OriginalFileName { get; set; }

    [StringLength(32)]
    public string? ChecksumMd5 { get; set; }

    [StringLength(64)]
    public string? ChecksumSha256 { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsPrimary { get; set; } = false;

    // Navigation properties
    [JsonIgnore]
    public virtual Verification Verification { get; set; } = null!;
}