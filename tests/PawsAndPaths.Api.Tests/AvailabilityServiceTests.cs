using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task DaySchedule_DistinguishesBookedBlockedAndBookableTimeWithoutPrivateData()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "Drop-in visit", Description = "Pet care",
            DurationMinutes = 30, Price = 22m, IsActive = true
        };
        db.Services.Add(service);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date, StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0), IsAvailable = false
        });
        db.Bookings.Add(new Booking
        {
            UserId = "private-user", DogId = 99, ServiceOfferingId = service.Id,
            Date = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0),
            Price = 22m, Status = BookingStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var schedule = await new AvailabilityService(db)
            .GetDayScheduleAsync(date, service.Id, CancellationToken.None);

        Assert.Equal(48, schedule.Count);
        Assert.Equal("Booked", schedule.Single(item => item.StartTime == new TimeOnly(10, 0)).Status);
        Assert.Equal("Unavailable", schedule.Single(item => item.StartTime == new TimeOnly(14, 0)).Status);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(12, 0)).IsBookable);
        Assert.All(schedule, item =>
            Assert.DoesNotContain("private", $"{item.StartTime}{item.Status}{item.IsBookable}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DaySchedule_DoesNotOfferAStartThatWouldOverlapABooking()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "One-hour visit", Description = "Pet care",
            DurationMinutes = 60, Price = 35m, IsActive = true
        };
        db.Services.Add(service);
        db.Bookings.Add(new Booking
        {
            UserId = "user", DogId = 1, ServiceOfferingId = service.Id,
            Date = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 30),
            Price = 35m, Status = BookingStatus.Pending
        });
        await db.SaveChangesAsync();

        var schedule = await new AvailabilityService(db)
            .GetDayScheduleAsync(date, service.Id, CancellationToken.None);

        Assert.False(schedule.Single(item => item.StartTime == new TimeOnly(9, 30)).IsBookable);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(10, 30)).IsBookable);
    }

    [Fact]
    public async Task Slots_BulkCalendarQueryRespectsBookingsAndBlocks()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "Drop-in visit", Description = "Pet care",
            DurationMinutes = 30, Price = 22m, IsActive = true
        };
        db.Services.Add(service);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date.AddDays(1), StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0), IsAvailable = false
        });
        db.Bookings.Add(new Booking
        {
            UserId = "user", DogId = 1, ServiceOfferingId = service.Id,
            Date = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 30),
            Price = 22m, Status = BookingStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var slots = await new AvailabilityService(db)
            .GetSlotsAsync(date, date.AddDays(1), service.Id, CancellationToken.None);

        Assert.DoesNotContain(slots, item => item.Date == date && item.StartTime == new TimeOnly(10, 0));
        Assert.DoesNotContain(slots, item => item.Date == date.AddDays(1) && item.StartTime == new TimeOnly(14, 0));
        Assert.Contains(slots, item => item.Date == date && item.StartTime == new TimeOnly(10, 30));
    }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
