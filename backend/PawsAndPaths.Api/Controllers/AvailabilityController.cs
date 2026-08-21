using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/availability")]
public class AvailabilityController(AppDbContext db, IAvailabilityService availabilityService) : ControllerBase
{
    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyList<AvailableSlotDto>>> Slots(
        DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken) =>
        Ok(await availabilityService.GetSlotsAsync(from, to, serviceId, cancellationToken));

    [HttpGet, Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<IReadOnlyList<AvailabilityDto>>> GetRules(CancellationToken cancellationToken) =>
        Ok((await db.Availability.AsNoTracking().Where(rule => !rule.IsAvailable)
            .OrderBy(rule => rule.SpecificDate).ThenBy(rule => rule.DayOfWeek)
            .ThenBy(rule => rule.StartTime).ToListAsync(cancellationToken)).Select(rule => rule.ToDto()));

    [HttpPost, Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<AvailabilityDto>> Create(AvailabilityWriteDto request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var rule = new AvailabilityRule();
        Apply(rule, request);
        db.Availability.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule.ToDto());
    }

    [HttpPut("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<AvailabilityDto>> Update(int id, AvailabilityWriteDto request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var rule = await db.Availability.FindAsync([id], cancellationToken);
        if (rule is null) return NotFound();
        Apply(rule, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rule.ToDto());
    }

    [HttpDelete("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var rule = await db.Availability.FindAsync([id], cancellationToken);
        if (rule is null) return NotFound();
        db.Availability.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool Validate(AvailabilityWriteDto request)
    {
        if ((request.DayOfWeek is null) == (request.SpecificDate is null))
            ModelState.AddModelError("scope", "Choose either a recurring weekday or one specific date.");
        if (request.EndTime <= request.StartTime)
            ModelState.AddModelError(nameof(request.EndTime), "End time must be after start time.");
        return ModelState.IsValid;
    }

    private static void Apply(AvailabilityRule rule, AvailabilityWriteDto request)
    {
        rule.DayOfWeek = request.DayOfWeek;
        rule.SpecificDate = request.SpecificDate;
        rule.StartTime = request.StartTime;
        rule.EndTime = request.EndTime;
        // The business is available 24/7 by default, so persisted rules are
        // owner-created exceptions that mark time as unavailable.
        rule.IsAvailable = false;
        rule.Notes = request.Notes?.Trim() ?? string.Empty;
    }
}
