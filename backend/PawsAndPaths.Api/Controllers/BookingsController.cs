using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Authorize]
[Route("api/bookings")]
public class BookingsController(AppDbContext db, IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bookings = await Query().Where(booking => booking.UserId == userId)
            .OrderByDescending(booking => booking.Date).ThenBy(booking => booking.StartTime)
            .ToListAsync(cancellationToken);
        return Ok(bookings.Select(booking => booking.ToDto()));
    }

    [HttpGet("admin"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetAll(CancellationToken cancellationToken)
    {
        var bookings = await Query().OrderBy(booking => booking.Date).ThenBy(booking => booking.StartTime)
            .ToListAsync(cancellationToken);
        return Ok(bookings.Select(booking => booking.ToDto()));
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingDto request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (booking, error) = await bookingService.CreateAsync(userId, request, cancellationToken);
        if (booking is null) return Conflict(new { message = error });
        var complete = await Query().SingleAsync(item => item.Id == booking.Id, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { id = booking.Id }, complete.ToDto());
    }

    [HttpPost("admin"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<BookingDto>> CreateForCustomer(
        CreateOwnerBookingDto request, CancellationToken cancellationToken)
    {
        var bookingRequest = new CreateBookingDto(
            request.DogId, request.ServiceId, request.Date,
            request.StartTime, request.SpecialInstructions);
        var (booking, error) = await bookingService.CreateAsync(
            request.CustomerId, bookingRequest, cancellationToken, BookingStatus.Confirmed);
        if (booking is null) return Conflict(new { message = error });
        var complete = await Query().SingleAsync(item => item.Id == booking.Id, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = booking.Id }, complete.ToDto());
    }

    [HttpPut("{id:int}/status"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<BookingDto>> ChangeStatus(
        int id, UpdateBookingStatusDto request, CancellationToken cancellationToken)
    {
        var allowed = new[] { BookingStatus.Confirmed, BookingStatus.Declined, BookingStatus.Cancelled, BookingStatus.Completed };
        if (!allowed.Contains(request.Status)) return BadRequest(new { message = "Invalid owner status change." });
        var (booking, error) = await bookingService.ChangeStatusAsync(id, request.Status, cancellationToken);
        return booking is null ? Conflict(new { message = error }) : Ok(booking.ToDto());
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelMine(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (booking is null) return NotFound();
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            return Conflict(new { message = "This booking can no longer be cancelled." });
        if (booking.Date < DateOnly.FromDateTime(DateTime.Today.AddDays(1)))
            return Conflict(new { message = "Please contact Princess Dog Walker for same-day cancellations." });
        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<Booking> Query() => db.Bookings.AsNoTracking()
        .Include(booking => booking.User).Include(booking => booking.Dog).Include(booking => booking.ServiceOffering);
}
