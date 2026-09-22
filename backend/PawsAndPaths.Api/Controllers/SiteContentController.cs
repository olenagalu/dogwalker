using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/site-content")]
public class SiteContentController(AppDbContext db) : ControllerBase
{
    private const string AboutPhotoKey = "about-photo";
    private const string OwnerDescriptionKey = "owner-description";
    private const int MaximumPhotoBytes = 5 * 1024 * 1024;

    [HttpGet("about-photo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAboutPhoto(CancellationToken cancellationToken)
    {
        var photo = await db.SiteContent.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == AboutPhotoKey, cancellationToken);
        if (photo is null) return NotFound();

        var etag = $"\"{photo.UpdatedAt.UtcTicks:x}\"";
        if (Request.Headers.IfNoneMatch.ToString() == etag)
            return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Data, photo.ContentType);
    }

    [HttpGet("owner-description"), AllowAnonymous]
    public async Task<IActionResult> GetOwnerDescription(CancellationToken cancellationToken)
    {
        var content = await db.SiteContent.AsNoTracking().SingleOrDefaultAsync(
            item => item.Key == OwnerDescriptionKey, cancellationToken);
        return Ok(new { text = content?.Text ?? "Hi, I’m Julia. I provide thoughtful, dependable care tailored to every dog and family." });
    }

    [HttpPut("owner-description"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> UpdateOwnerDescription([FromBody] OwnerDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length is < 1 or > 3000) return BadRequest(new { message = "Description must be between 1 and 3,000 characters." });
        var content = await db.SiteContent.SingleOrDefaultAsync(item => item.Key == OwnerDescriptionKey, cancellationToken);
        if (content is null) { content = new SiteContent { Key = OwnerDescriptionKey }; db.SiteContent.Add(content); }
        content.Text = text;
        content.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { content.Text, content.UpdatedAt });
    }

    [HttpPut("about-photo")]
    [Authorize(Roles = AppRoles.Owner)]
    [RequestSizeLimit(MaximumPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> UpdateAboutPhoto(
        [FromForm] IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { message = "Choose a photo to upload." });
        if (photo.Length > MaximumPhotoBytes)
            return BadRequest(new { message = "The photo must be 5 MB or smaller." });

        await using var stream = new MemoryStream((int)photo.Length);
        await photo.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        var contentType = DetectImageType(bytes);
        if (contentType is null)
            return BadRequest(new { message = "Use a JPEG, PNG, or WebP photo." });

        var saved = await db.SiteContent.SingleOrDefaultAsync(
            item => item.Key == AboutPhotoKey, cancellationToken);
        if (saved is null)
        {
            saved = new SiteContent { Key = AboutPhotoKey };
            db.SiteContent.Add(saved);
        }

        saved.Data = bytes;
        saved.ContentType = contentType;
        saved.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { updatedAt = saved.UpdatedAt });
    }

    private static string? DetectImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return "image/png";
        if (bytes.Length >= 12
            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}

public record OwnerDescriptionRequest(string? Text);
