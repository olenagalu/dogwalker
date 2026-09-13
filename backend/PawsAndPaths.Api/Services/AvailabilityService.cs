using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface IAvailabilityService
{
    Task<bool> IsAvailableAsync(DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId,
        CancellationToken cancellationToken, bool enforceRegularHours = true);
    Task<IReadOnlyList<AvailableSlotDto>> GetSlotsAsync(DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicScheduleSegmentDto>> GetDayScheduleAsync(DateOnly date, int serviceId, CancellationToken cancellationToken);
}

public class AvailabilityService(AppDbContext db) : IAvailabilityService
{
    private static readonly TimeOnly RegularDayStart = new(6, 0);
    private static readonly TimeOnly RegularDayEnd = new(23, 0);

    public async Task<bool> IsAvailableAsync(
        DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId,
        CancellationToken cancellationToken, bool enforceRegularHours = true)
    {
        if (end <= start || date < DateOnly.FromDateTime(DateTime.Today)) return false;
        if (enforceRegularHours && (start < RegularDayStart || end > RegularDayEnd)) return false;

        // Within regular hours, owner-created rules remove additional time.
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
        if (service.IsOvernightStay) return [];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var blockedRules = await db.Availability.AsNoTracking()
            .Where(rule => !rule.IsAvailable).ToListAsync(cancellationToken);
        var activeBookings = await db.Bookings.AsNoTracking()
            .Where(booking => (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
                && ((!booking.IsOvernightStay && booking.Date >= from && booking.Date <= to)
                    || (booking.IsOvernightStay && booking.Date <= to && booking.EndDate >= from)))
            .ToListAsync(cancellationToken);
        var bookingWindows = activeBookings.SelectMany(BookingSchedule.Windows)
            .Where(window => window.Date >= from && window.Date <= to)
            .GroupBy(window => window.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        var slots = new List<AvailableSlotDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (date < today) continue;
            var blocked = blockedRules.Where(rule =>
                rule.SpecificDate == date || rule.DayOfWeek == date.DayOfWeek).ToList();
            var windows = bookingWindows.GetValueOrDefault(date, []);
            // Regular appointments begin no earlier than 6 AM and must finish
            // by 11 PM. Overnight stays use their separate care schedule.
            for (var startMinutes = 6 * 60;
                 startMinutes + service.DurationMinutes <= 23 * 60;
                 startMinutes += 30)
            {
                var start = new TimeOnly(startMinutes / 60, startMinutes % 60);
                var endMinutes = startMinutes + service.DurationMinutes;
                var end = new TimeOnly(endMinutes / 60, endMinutes % 60);
                if (!blocked.Any(rule => Overlaps(start, end, rule.StartTime, rule.EndTime))
                    && !windows.Any(window => Overlaps(start, end, window.StartTime, window.EndTime)))
                    slots.Add(new AvailableSlotDto(date, start, end));
            }
        }
        return slots.Distinct().OrderBy(slot => slot.Date).ThenBy(slot => slot.StartTime).ToList();
    }

    public async Task<IReadOnlyList<PublicScheduleSegmentDto>> GetDayScheduleAsync(
        DateOnly date, int serviceId, CancellationToken cancellationToken)
    {
        var service = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsActive, cancellationToken);
        if (service is null) return [];

        var blocked = await RulesForDate(date).Where(rule => !rule.IsAvailable)
            .ToListAsync(cancellationToken);
        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
                && ((!booking.IsOvernightStay && booking.Date == date)
                    || (booking.IsOvernightStay && booking.Date <= date && booking.EndDate >= date)))
            .ToListAsync(cancellationToken);
        var bookingWindows = bookings.SelectMany(BookingSchedule.Windows)
            .Where(window => window.Date == date).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var segments = new List<PublicScheduleSegmentDto>();

        for (var startMinutes = 0; startMinutes < 24 * 60; startMinutes += 30)
        {
            var start = new TimeOnly(startMinutes / 60, startMinutes % 60);
            var segmentEnd = startMinutes == 23 * 60 + 30
                ? TimeOnly.MaxValue
                : start.AddMinutes(30);
            var booked = bookingWindows.Any(window =>
                Overlaps(start, segmentEnd, window.StartTime, window.EndTime));
            var unavailable = blocked.Any(rule =>
                Overlaps(start, segmentEnd, rule.StartTime, rule.EndTime));
            var withinRegularHours = service.IsOvernightStay
                || (start >= RegularDayStart && segmentEnd <= RegularDayEnd);
            var status = booked ? "Booked" : unavailable || !withinRegularHours ? "Unavailable" : "Available";

            var appointmentEndMinutes = startMinutes + service.DurationMinutes;
            var appointmentFitsHours = start >= RegularDayStart && appointmentEndMinutes <= 23 * 60;
            var bookable = !service.IsOvernightStay && date >= today
                && status == "Available" && appointmentFitsHours;
            if (bookable)
            {
                var appointmentEnd = new TimeOnly(appointmentEndMinutes / 60, appointmentEndMinutes % 60);
                bookable = !bookingWindows.Any(window =>
                        Overlaps(start, appointmentEnd, window.StartTime, window.EndTime))
                    && !blocked.Any(rule =>
                        Overlaps(start, appointmentEnd, rule.StartTime, rule.EndTime));
            }

            segments.Add(new PublicScheduleSegmentDto(start, status, bookable));
        }

        return segments;
    }

    private IQueryable<AvailabilityRule> RulesForDate(DateOnly date) =>
        db.Availability.Where(rule => rule.SpecificDate == date || rule.DayOfWeek == date.DayOfWeek);

    private static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && endA > startB;
}
