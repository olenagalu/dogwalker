using System.Data;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface IBookingService
{
    Task<(Booking? Booking, string? Error)> CreateAsync(
        string userId, CreateBookingDto request, CancellationToken cancellationToken,
        BookingStatus initialStatus = BookingStatus.Pending);
    Task<(Booking? Booking, string? Error)> ChangeStatusAsync(int bookingId, BookingStatus status, CancellationToken cancellationToken);
}

public class BookingService(AppDbContext db, IAvailabilityService availability) : IBookingService
{
    public async Task<(Booking? Booking, string? Error)> CreateAsync(
        string userId, CreateBookingDto request, CancellationToken cancellationToken,
        BookingStatus initialStatus = BookingStatus.Pending)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var dog = await db.Dogs.SingleOrDefaultAsync(item => item.Id == request.DogId && item.UserId == userId, cancellationToken);
        if (dog is null) return (null, "The selected dog does not belong to this account.");

        var service = await db.Services.SingleOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive, cancellationToken);
        if (service is null) return (null, "The selected service is unavailable.");
        if (request.Date < DateOnly.FromDateTime(DateTime.Today)) return (null, "Booking dates cannot be in the past.");

        var end = request.StartTime.AddMinutes(service.DurationMinutes);
        if (!await availability.IsAvailableAsync(request.Date, request.StartTime, end, null, cancellationToken))
            return (null, "That time is no longer available.");

        var booking = new Booking
        {
            UserId = userId,
            DogId = dog.Id,
            ServiceOfferingId = service.Id,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = end,
            Price = service.Price,
            SpecialInstructions = request.SpecialInstructions?.Trim() ?? string.Empty,
            Status = initialStatus
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return (booking, null);
    }

    public async Task<(Booking? Booking, string? Error)> ChangeStatusAsync(
        int bookingId, BookingStatus status, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings
            .Include(item => item.User).Include(item => item.Dog).Include(item => item.ServiceOffering)
            .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        if (booking is null) return (null, "Booking not found.");

        if (status == BookingStatus.Confirmed
            && !await availability.IsAvailableAsync(booking.Date, booking.StartTime, booking.EndTime, booking.Id, cancellationToken))
            return (null, "This booking now conflicts with availability or another active booking.");

        booking.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return (booking, null);
    }
}
