using Google.Apis.Auth;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
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
    IOwnerNotificationEmailSender ownerNotificationEmailSender,
    AppDbContext dbContext,
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
        await TrySendAccountApprovalRequestAsync(user);
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
            await TrySendAccountApprovalRequestAsync(user);
        }

        return Ok(await ResponseFor(user));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(ForgotPasswordDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        const string responseMessage = "If that account exists, a 6-digit verification code has been sent.";
        if (user is null) return Ok(new ForgotPasswordResponseDto(responseMessage));

        var oldCodes = await dbContext.PasswordResetCodes
            .Where(item => item.UserId == user.Id && item.UsedAt == null)
            .ToListAsync();
        oldCodes.ForEach(item => item.UsedAt = DateTimeOffset.UtcNow);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        dbContext.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = HashResetCode(user.Id, code),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        });
        await dbContext.SaveChangesAsync();

        var expose = configuration.GetValue<bool>("ExposePasswordResetTokens");
        if (!expose)
        {
            try { await passwordResetEmailSender.SendAsync(user.Email!, user.FullName, code); }
            catch (Exception exception) { logger.LogError(exception, "Password reset email could not be sent for user {UserId}.", user.Id); }
        }
        return Ok(new ForgotPasswordResponseDto(responseMessage, expose ? code : null));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return BadRequest(new { message = "The reset request is invalid." });

        var resetCode = await dbContext.PasswordResetCodes
            .Where(item => item.UserId == user.Id && item.UsedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();
        if (resetCode is null || resetCode.ExpiresAt <= DateTimeOffset.UtcNow || resetCode.FailedAttempts >= 5)
            return BadRequest(new { message = "The verification code is invalid or has expired." });
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(resetCode.CodeHash),
                Convert.FromHexString(HashResetCode(user.Id, request.Code))))
        {
            resetCode.FailedAttempts++;
            await dbContext.SaveChangesAsync();
            return BadRequest(new { message = "The verification code is invalid or has expired." });
        }

        var identityToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, identityToken, request.NewPassword);
        if (result.Succeeded)
        {
            resetCode.UsedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
        }
        return result.Succeeded
            ? Ok(new { message = "Password reset. You can now sign in." })
            : ValidationProblem(new ValidationProblemDetails(
                result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
    }

    private string HashResetCode(string userId, string code)
    {
        var secret = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{code}:{secret}")));
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

    private async Task TrySendAccountApprovalRequestAsync(AppUser user)
    {
        try
        {
            await ownerNotificationEmailSender.SendAccountApprovalRequestAsync(
                new AccountApprovalNotification(
                    user.FullName,
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty,
                    user.ServiceArea,
                    user.ServiceAddress));
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Owner account-approval notification could not be sent for user {UserId}.", user.Id);
        }
    }
}
