using DocumentVerification.API.Models.DTOs.Auth;

namespace DocumentVerification.API.Services;

public interface IAuthService
{
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string userId);
    Task<bool> ValidateTokenAsync(string token);
    Task<string> GenerateRefreshTokenAsync(string userId);
    Task<UserDto> GetUserByIdAsync(string userId);
}