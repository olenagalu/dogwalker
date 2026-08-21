using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface IAvailabilityService
{
    Task<bool> IsAvailableAsync(DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableSlotDto>> GetSlotsAsync(DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken);
}

public class AvailabilityService(AppDbContext db) : IAvailabilityService
{
    public async Task<bool> IsAvailableAsync(
        DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId,
        CancellationToken cancellationToken)
    {
        if (end <= start || date < DateOnly.FromDateTime(DateTime.Today)) return false;

        // Princess Dog Walker is open around the clock by default. Availability
        // rules are therefore exceptions: an owner-created block removes time.
        var blocked = await RulesForDate(date).Where(rule => !rule.IsAvailable).ToListAsync(cancellationToken);
        if (blocked.Any(rule => Overlaps(start, end, rule.StartTime, rule.EndTime))) return false;

        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => booking.Id != excludeBookingId
                && (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
                && ((!booking.IsOvernightStay && booking.Date == date)
                    || (booking.IsOvernightStay && booking.Date <= date && booking.EndDate >= date)))
            .ToListAsync(cancellationToken);
        return !bookings.SelectMany(BookingSchedule.Windows)
            .Any(window => window.Date == date && Overlaps(start, end, window.StartTime, window.EndTime));
    }

    public async Task<IReadOnlyList<AvailableSlotDto>> GetSlotsAsync(
        DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken)
    {
        if (to < from || to.DayNumber - from.DayNumber > 60) return [];
        var service = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsActive, cancellationToken);
        if (service is null) return [];

        var slots = new List<AvailableSlotDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            // Generate half-hour starts throughout the day. Appointments that
            // would cross midnight are offered on the following date instead.
            for (var startMinutes = 0; startMinutes + service.DurationMinutes < 24 * 60; startMinutes += 30)
            {
                var start = new TimeOnly(startMinutes / 60, startMinutes % 60);
                var endMinutes = startMinutes + service.DurationMinutes;
                var end = new TimeOnly(endMinutes / 60, endMinutes % 60);
                if (await IsAvailableAsync(date, start, end, null, cancellationToken))
                    slots.Add(new AvailableSlotDto(date, start, end));
            }
        }
        return slots.Distinct().OrderBy(slot => slot.Date).ThenBy(slot => slot.StartTime).ToList();
    }

    private IQueryable<AvailabilityRule> RulesForDate(DateOnly date) =>
        db.Availability.Where(rule => rule.SpecificDate == date || rule.DayOfWeek == date.DayOfWeek);

    private static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && endA > startB;
}
