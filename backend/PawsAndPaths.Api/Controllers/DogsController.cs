using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Authorize]
[Route("api/dogs")]
public class DogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DogDto>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok((await db.Dogs.AsNoTracking().Where(dog => dog.UserId == userId)
            .OrderBy(dog => dog.Name).ToListAsync(cancellationToken)).Select(dog => dog.ToDto()));
    }

    [HttpPost]
    public async Task<ActionResult<DogDto>> Create(DogWriteDto request, CancellationToken cancellationToken)
    {
        var dog = new Dog { UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, Name = request.Name.Trim() };
        Apply(dog, request);
        db.Dogs.Add(dog);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dog.Id }, dog.ToDto());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DogDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        return dog is null ? NotFound() : Ok(dog.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DogDto>> Update(int id, DogWriteDto request, CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        if (dog is null) return NotFound();
        Apply(dog, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(dog.ToDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        if (dog is null) return NotFound();
        if (await db.Bookings.AnyAsync(booking => booking.DogId == id, cancellationToken))
            return Conflict(new { message = "Dogs with booking history cannot be deleted." });
        db.Dogs.Remove(dog);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<Dog?> FindOwned(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return db.Dogs.SingleOrDefaultAsync(dog => dog.Id == id && dog.UserId == userId, cancellationToken);
    }

    private static void Apply(Dog dog, DogWriteDto request)
    {
        dog.Name = request.Name.Trim();
        dog.Breed = request.Breed?.Trim() ?? string.Empty;
        dog.Age = request.Age;
        dog.CareInstructions = request.CareInstructions?.Trim() ?? string.Empty;
        dog.BehavioralNotes = request.BehavioralNotes?.Trim() ?? string.Empty;
        dog.MedicalNotes = request.MedicalNotes?.Trim() ?? string.Empty;
    }
}
