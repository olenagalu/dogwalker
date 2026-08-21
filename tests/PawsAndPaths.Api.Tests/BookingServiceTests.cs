using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBooking_UsesServiceDurationAndPrice()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), "Use the pink leash."),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(booking);
        Assert.Equal(new TimeOnly(10, 30), booking.EndTime);
        Assert.Equal(24m, booking.Price);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task CreateBooking_PreventsOverlappingActiveBooking()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));
        await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        var secondUser = new AppUser { Id = "user-2", UserName = "second@example.com", Email = "second@example.com", FullName = "Second Customer" };
        var secondDog = new Dog { UserId = secondUser.Id, User = secondUser, Name = "Scout" };
        db.Users.Add(secondUser);
        db.Dogs.Add(secondDog);
        await db.SaveChangesAsync();
        var (booking, error) = await bookingService.CreateAsync(secondUser.Id,
            new CreateBookingDto(secondDog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        Assert.Null(booking);
        Assert.Equal("That time is no longer available.", error);
        Assert.Single(await db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_AllowsAnyOpenTimeWithoutAvailabilityRules()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(21, 30), null),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(booking);
    }

    [Fact]
    public async Task CreateBooking_RejectsOwnerBlockedTime()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(16, 0),
            IsAvailable = false
        });
        await db.SaveChangesAsync();
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(15, 0), null),
            CancellationToken.None);

        Assert.Null(booking);
        Assert.Equal("That time is no longer available.", error);
    }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(AppUser User, Dog Dog, ServiceOffering Service, DateOnly Date)> Seed(AppDbContext db)
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        var user = new AppUser { Id = "user-1", UserName = "sam@example.com", Email = "sam@example.com", FullName = "Sam Taylor" };
        var dog = new Dog { UserId = user.Id, User = user, Name = "Mabel", Breed = "Beagle mix" };
        var service = new ServiceOffering { Id = 101, Name = "30-minute dog walk", Description = "A happy walk", DurationMinutes = 30, Price = 24m, IsActive = true };
        db.Users.Add(user);
        db.Dogs.Add(dog);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return (user, dog, service, date);
    }
}
