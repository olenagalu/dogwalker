using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Controllers;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Tests;

public class SiteContentControllerTests
{
    [Fact]
    public void UpdateAboutPhoto_IsOwnerOnly()
    {
        var method = typeof(SiteContentController).GetMethod(nameof(SiteContentController.UpdateAboutPhoto));
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AppRoles.Owner, authorize.Roles);
    }

    [Fact]
    public async Task UpdateAboutPhoto_SavesDetectedImageType()
    {
        await using var db = CreateDatabase();
        var controller = Controller(db);
        var bytes = new byte[] { 0xff, 0xd8, 0xff, 0xe0, 0x00 };
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photo", "julia.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };

        var result = await controller.UpdateAboutPhoto(file, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.SiteContent.SingleAsync();
        Assert.Equal("about-photo", saved.Key);
        Assert.Equal("image/jpeg", saved.ContentType);
        Assert.Equal(bytes, saved.Data);
    }

    [Fact]
    public async Task UpdateAboutPhoto_RejectsNonImageContent()
    {
        await using var db = CreateDatabase();
        var controller = Controller(db);
        var bytes = "not an image"u8.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photo", "notes.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.UpdateAboutPhoto(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.SiteContent);
    }

    [Fact]
    public async Task GetAboutPhoto_ReturnsSavedBytesPublicly()
    {
        await using var db = CreateDatabase();
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        db.SiteContent.Add(new SiteContent
        {
            Key = "about-photo",
            ContentType = "image/png",
            Data = bytes
        });
        await db.SaveChangesAsync();

        var result = await Controller(db).GetAboutPhoto(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(bytes, file.FileContents);
    }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SiteContentController Controller(AppDbContext db) => new(db)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };
}
