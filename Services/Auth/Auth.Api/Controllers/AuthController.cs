using Auth.Api.Constants;
using Auth.Api.Contracts;
using Auth.Api.Extensions;
using Auth.Api.Models;
using Auth.Api.Services;
using Auth.Api.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IJwtTokenService jwt,
    IRefreshTokenService refreshTokens,
    IEmailService email,
    IOptions<JwtSettings> settings) : ControllerBase
{
    private const string RefreshCookie = "refreshToken";

    // ---------- Реєстрація та підтвердження email ----------

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Username, Email = request.Email };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return this.IdentityProblem(created);

        await users.AddToRoleAsync(user, AppRoles.User);   // роль User автоматично
        await SendVerificationEmailAsync(user);

        return StatusCode(StatusCodes.Status201Created, new
        {
            user.Id,
            user.UserName,
            user.Email,
            Message = "Registered. Confirm your email before logging in."
        });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return Problem(statusCode: 400, title: "Invalid or expired token");

        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded) return Problem(statusCode: 400, title: "Invalid or expired token");

        await users.UpdateSecurityStampAsync(user);   // токен стає одноразовим
        return Ok(new { Message = "Email confirmed" });
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && !user.EmailConfirmed)
            await SendVerificationEmailAsync(user);

        // Однакова відповідь завжди: не розкриваємо, чи існує такий email
        return Ok(new { Message = "If the account exists and is not confirmed, a new email was sent." });
    }

    // ---------- Login / refresh / revoke / logout ----------

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.Login) ?? await users.FindByEmailAsync(request.Login);
        if (user is null) return Problem(statusCode: 401, title: "Invalid credentials");

        // lockoutOnFailure: після 5 невдалих спроб акаунт блокується на 5 хв
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return Problem(statusCode: StatusCodes.Status423Locked, title: "Account is locked. Try again later.");
        if (!check.Succeeded)
            return Problem(statusCode: 401, title: "Invalid credentials");

        if (!user.EmailConfirmed)
            return Problem(statusCode: 403, title: "Email is not confirmed");

        SetRefreshCookie(await refreshTokens.IssueAsync(user, ct));   // refresh -> HttpOnly cookie
        return Ok(await CreateAccessAsync(user));                     // access -> тіло відповіді
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var raw) || string.IsNullOrEmpty(raw))
            return Problem(statusCode: 401, title: "Refresh token is missing");

        var rotated = await refreshTokens.RotateAsync(raw, ct);
        if (rotated is null)
        {
            ClearRefreshCookie();
            return Problem(statusCode: 401, title: "Refresh token is invalid, expired or revoked");
        }

        SetRefreshCookie(rotated.RefreshToken);
        return Ok(await CreateAccessAsync(rotated.User));
    }

    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> Revoke(RevokeTokenRequest? request, CancellationToken ct)
    {
        var token = request?.Token ?? Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(token)) return Problem(statusCode: 400, title: "Token is required");

        var revoked = await refreshTokens.RevokeAsync(token, User.GetUserId()!, ct);
        return revoked ? NoContent() : Problem(statusCode: 404, title: "Token not found or already revoked");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var token) && !string.IsNullOrEmpty(token))
            await refreshTokens.RevokeAsync(token, User.GetUserId()!, ct);

        ClearRefreshCookie();
        return NoContent();
    }

    // ---------- Паролі ----------

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && user.EmailConfirmed)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await email.SendAsync(user.Email!, "Password reset",
                $"Use POST /auth/reset-password with email and this token (valid 2 hours):{Environment.NewLine}{token}");
        }

        return Ok(new { Message = "If the account exists, a reset email was sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return Problem(statusCode: 400, title: "Invalid or expired token");

        // Після успіху змінюється security stamp, тому токен скидання одноразовий
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) return this.IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(user.Id, ct);   // усі сесії відкликаємо
        return Ok(new { Message = "Password has been reset" });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(User.GetUserId()!);
        if (user is null) return Problem(statusCode: 401, title: "User not found");

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return this.IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(user.Id, ct);
        ClearRefreshCookie();
        return Ok(new { Message = "Password changed. Please log in again." });
    }

    // ---------- Допоміжне ----------

    private async Task SendVerificationEmailAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await email.SendAsync(user.Email!, "Confirm your email",
            $"Use POST /auth/verify-email with email and this token (valid 2 hours):{Environment.NewLine}{token}");
    }

    private async Task<AuthResponse> CreateAccessAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new AuthResponse(jwt.GenerateAccessToken(user, roles), settings.Value.AccessTokenMinutes * 60);
    }

    private void SetRefreshCookie(string token)
        => Response.Cookies.Append(RefreshCookie, token, new CookieOptions
        {
            HttpOnly = true,                       // недоступний з JavaScript (захист від XSS)
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/auth",                        // cookie їде лише на /auth/*
            Expires = DateTimeOffset.UtcNow.AddDays(settings.Value.RefreshTokenDays)
        });

    private void ClearRefreshCookie()
        => Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/auth" });
}