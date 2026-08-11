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

        var rules = await RulesForDate(date).ToListAsync(cancellationToken);
        var specificAvailable = rules.Where(rule => rule.SpecificDate == date && rule.IsAvailable).ToList();
        var available = specificAvailable.Count > 0
            ? specificAvailable
            : rules.Where(rule => rule.SpecificDate is null && rule.IsAvailable).ToList();

        if (!available.Any(rule => start >= rule.StartTime && end <= rule.EndTime)) return false;
        if (rules.Any(rule => !rule.IsAvailable && Overlaps(start, end, rule.StartTime, rule.EndTime))) return false;

        return !await db.Bookings.AnyAsync(booking =>
            booking.Date == date
            && booking.Id != excludeBookingId
            && (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
            && start < booking.EndTime && end > booking.StartTime,
            cancellationToken);
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
            var rules = await RulesForDate(date).AsNoTracking().ToListAsync(cancellationToken);
            var specific = rules.Where(rule => rule.SpecificDate == date && rule.IsAvailable).ToList();
            var available = specific.Count > 0 ? specific : rules.Where(rule => rule.SpecificDate is null && rule.IsAvailable).ToList();
            foreach (var rule in available)
            {
                for (var start = rule.StartTime; start.AddMinutes(service.DurationMinutes) <= rule.EndTime; start = start.AddMinutes(30))
                {
                    var end = start.AddMinutes(service.DurationMinutes);
                    if (await IsAvailableAsync(date, start, end, null, cancellationToken))
                        slots.Add(new AvailableSlotDto(date, start, end));
                }
            }
        }
        return slots.Distinct().OrderBy(slot => slot.Date).ThenBy(slot => slot.StartTime).ToList();
    }

    private IQueryable<AvailabilityRule> RulesForDate(DateOnly date) =>
        db.Availability.Where(rule => rule.SpecificDate == date || rule.DayOfWeek == date.DayOfWeek);

    private static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && endA > startB;
}
