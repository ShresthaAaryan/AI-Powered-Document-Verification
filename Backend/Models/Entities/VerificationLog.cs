using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DocumentVerification.API.Models.Entities;

public class VerificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VerificationId { get; set; }

    public string? UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ServiceName { get; set; }

    [StringLength(20)]
    public string? PreviousStatus { get; set; }

    [StringLength(20)]
    public string? NewStatus { get; set; }

    public string? Details { get; set; } // JSON string

    public System.Net.IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public int? ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore]
    public virtual Verification Verification { get; set; } = null!;
}