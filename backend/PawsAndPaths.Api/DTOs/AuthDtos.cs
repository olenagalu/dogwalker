using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.DTOs;

public record RegisterDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, Phone, MaxLength(30)] string Phone,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record GoogleSignInDto([Required] string Credential);

public record AuthResponseDto(string Token, DateTimeOffset ExpiresAt, UserProfileDto User);

public record ForgotPasswordDto([Required, EmailAddress] string Email);

public record ForgotPasswordResponseDto(string Message, string? ResetToken = null);

public record ResetPasswordDto(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(8), MaxLength(100)] string NewPassword);

public record UpdateProfileDto(
    [Required, MaxLength(120)] string FullName,
    [Required, Phone, MaxLength(30)] string Phone);

public record UserProfileDto(string Id, string FullName, string Email, string Phone, string Role);

public record CustomerSummaryDto(string Id, string FullName, string Email, string Phone, IReadOnlyList<DogDto> Dogs);
