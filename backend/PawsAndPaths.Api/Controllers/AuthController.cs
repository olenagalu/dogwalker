using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[EnableRateLimiting("account")]
[Route("api/auth")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ITokenService tokenService,
    IWelcomeEmailSender welcomeEmailSender,
    IPasswordResetEmailSender passwordResetEmailSender,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Conflict(new { message = "An account with this email already exists." });

        var user = new AppUser
        {
            FullName = request.FullName.Trim(), UserName = email, Email = email,
            PhoneNumber = request.Phone.Trim(), ServiceArea = request.ServiceArea.Trim(),
            ServiceAddress = request.ServiceAddress.Trim(), ApprovalStatus = AccountApprovalStatus.Pending
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));

        await userManager.AddToRoleAsync(user, AppRoles.Customer);
        await TrySendWelcomeEmailAsync(user);
        return Ok(await ResponseFor(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !(await signInManager.CheckPasswordSignInAsync(user, request.Password, true)).Succeeded)
            return Unauthorized(new { message = "Email or password is incorrect." });
        return Ok(await ResponseFor(user));
    }

    [HttpGet("google-config")]
    public IActionResult GoogleConfig()
    {
        var clientId = configuration["Google:ClientId"];
        return Ok(new { enabled = !string.IsNullOrWhiteSpace(clientId), clientId });
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponseDto>> GoogleSignIn(GoogleSignInDto request)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Google sign-in is not configured yet." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Google could not verify this sign-in." });
        }

        var authoritative = payload.EmailVerified
            && (payload.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(payload.HostedDomain));
        if (!authoritative)
            return Unauthorized(new { message = "Please use email and password for this Google account address." });

        var email = payload.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name.Trim(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ApprovalStatus = AccountApprovalStatus.Pending
            };
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);
            await userManager.AddToRoleAsync(user, AppRoles.Customer);
            await TrySendWelcomeEmailAsync(user);
        }

        return Ok(await ResponseFor(user));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(ForgotPasswordDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Ok(new ForgotPasswordResponseDto("If that account exists, reset instructions are ready."));
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var expose = configuration.GetValue<bool>("ExposePasswordResetTokens");
        if (!expose)
        {
            var baseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";
            var resetUrl = $"{baseUrl}/auth.html?email={Uri.EscapeDataString(user.Email!)}&resetToken={Uri.EscapeDataString(token)}";
            try { await passwordResetEmailSender.SendAsync(user.Email!, user.FullName, resetUrl); }
            catch (Exception exception) { logger.LogError(exception, "Password reset email could not be sent for user {UserId}.", user.Id); }
        }
        return Ok(new ForgotPasswordResponseDto(
            "If that account exists, reset instructions are ready.", expose ? token : null));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return BadRequest(new { message = "The reset request is invalid." });
        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded
            ? Ok(new { message = "Password reset. You can now sign in." })
            : ValidationProblem(new ValidationProblemDetails(
                result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
    }

    private async Task<AuthResponseDto> ResponseFor(AppUser user)
    {
        var (token, expiresAt) = await tokenService.CreateAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.Owner) ? AppRoles.Owner : AppRoles.Customer;
        return new AuthResponseDto(token, expiresAt,
            new UserProfileDto(user.Id, user.FullName, user.Email ?? string.Empty,
                user.PhoneNumber ?? string.Empty, role, user.ServiceArea, user.ServiceAddress,
                user.ApprovalStatus, user.ProfilePhotoData.Length > 0));
    }

    private async Task TrySendWelcomeEmailAsync(AppUser user)
    {
        try
        {
            await welcomeEmailSender.SendAsync(user.Email!, user.FullName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Welcome email could not be sent for user {UserId}.", user.Id);
        }
    }
}
