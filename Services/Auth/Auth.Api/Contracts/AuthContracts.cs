using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginRequest([Required] string Login, [Required] string Password);   // username або email

public record AuthResponse(string AccessToken, int ExpiresInSeconds);

public record RevokeTokenRequest(string? Token);

public record VerifyEmailRequest([Required, EmailAddress] string Email, [Required] string Token);
public record ResendVerificationRequest([Required, EmailAddress] string Email);
public record ForgotPasswordRequest([Required, EmailAddress] string Email);
public record ResetPasswordRequest(
    [Required, EmailAddress] string Email, [Required] string Token, [Required] string NewPassword);
public record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);

public record UpdateUserRequest(
    [StringLength(100)] string? FullName,
    [Phone] string? PhoneNumber);

public record AssignRoleRequest([Required] string Role);

public record UserDto(
    string Id, string UserName, string Email, string? FullName,
    bool EmailConfirmed, IList<string> Roles, DateTime CreatedAtUtc);

public record RefreshTokenDto(
    int Id, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc, bool IsActive);