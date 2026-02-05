using System.ComponentModel.DataAnnotations;

namespace DocumentVerification.API.Models.DTOs.Verification;

public class CreateVerificationRequest
{
    [Required]
    [StringLength(50)]
    public string DocumentType { get; set; } = string.Empty; // Passport, DriversLicense, NationalID, CitizenshipCard

    [Required]
    public IFormFile IdDocument { get; set; } = null!;

    /// <summary>Optional. For CitizenshipCard only: back side (data in English).</summary>
    public IFormFile? IdDocumentBack { get; set; }

    [Required]
    public IFormFile SelfieImage { get; set; } = null!;

    [StringLength(100)]
    public string? ApplicantName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(100)]
    public string? ReferenceNumber { get; set; }

    [StringLength(10)]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
}