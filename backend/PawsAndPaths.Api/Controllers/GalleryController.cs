using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Route("api/gallery")]
public class GalleryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(
        await db.GalleryPhotos.AsNoTracking().OrderBy(item => item.DisplayOrder).ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.Caption }).ToListAsync(cancellationToken));

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
    {
        var photo = await db.GalleryPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=3600";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Data, photo.ContentType);
    }

    [HttpPost, Authorize(Roles = AppRoles.Owner), RequestSizeLimit(ImageUpload.MaximumBytes + 65536)]
    public async Task<IActionResult> Create([FromForm] IFormFile? photo, [FromForm] string? caption,
        CancellationToken cancellationToken)
    {
        if (await db.GalleryPhotos.CountAsync(cancellationToken) >= 30)
            return Conflict(new { message = "The gallery can contain up to 30 photos." });
        var upload = await ImageUpload.ReadAsync(photo, cancellationToken);
        if (upload.Error is not null) return BadRequest(new { message = upload.Error });
        var item = new GalleryPhoto { Caption = (caption ?? string.Empty).Trim(), Data = upload.Data!, ContentType = upload.ContentType! };
        db.GalleryPhotos.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { item.Id, item.Caption });
    }

    [HttpDelete("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await db.GalleryPhotos.FindAsync([id], cancellationToken);
        if (item is null) return NotFound();
        db.GalleryPhotos.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
