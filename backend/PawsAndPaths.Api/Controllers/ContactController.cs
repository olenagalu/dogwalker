using Microsoft.AspNetCore.Mvc;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(ContactMessageDto request, CancellationToken cancellationToken)
    {
        db.ContactMessages.Add(new ContactMessage
        {
            Name = request.Name.Trim(), Email = request.Email.Trim().ToLowerInvariant(), Message = request.Message.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Thanks for reaching out. Julia will be in touch soon." });
    }
}
