using System.ComponentModel.DataAnnotations;

namespace DocumentVerification.API.Models.DTOs.Auth;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
}