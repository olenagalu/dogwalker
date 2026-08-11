using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Dog> Dogs => Set<Dog>();
    public DbSet<ServiceOffering> Services => Set<ServiceOffering>();
    public DbSet<AvailabilityRule> Availability => Set<AvailabilityRule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasMany(user => user.Dogs).WithOne(dog => dog.User)
            .HasForeignKey(dog => dog.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppUser>()
            .HasMany(user => user.Bookings).WithOne(booking => booking.User)
            .HasForeignKey(booking => booking.UserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Dog>()
            .HasMany(dog => dog.Bookings).WithOne(booking => booking.Dog)
            .HasForeignKey(booking => booking.DogId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceOffering>()
            .HasMany(service => service.Bookings).WithOne(booking => booking.ServiceOffering)
            .HasForeignKey(booking => booking.ServiceOfferingId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>().Property(booking => booking.Status)
            .HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Booking>()
            .HasIndex(booking => new { booking.Date, booking.StartTime, booking.EndTime });

        modelBuilder.Entity<AvailabilityRule>()
            .HasIndex(rule => new { rule.SpecificDate, rule.DayOfWeek, rule.StartTime, rule.EndTime });
        modelBuilder.Entity<AvailabilityRule>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Availability_OneScope",
                "(\"SpecificDate\" IS NOT NULL) <> (\"DayOfWeek\" IS NOT NULL)"));
        modelBuilder.Entity<AvailabilityRule>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Availability_TimeRange",
                "\"EndTime\" > \"StartTime\""));

        modelBuilder.Entity<ServiceOffering>().HasData(
            new ServiceOffering { Id = 1, Name = "30-minute dog walk", Description = "A focused neighborhood walk with time to sniff, move, and reset.", DurationMinutes = 30, Price = 24m, IsActive = true },
            new ServiceOffering { Id = 2, Name = "60-minute dog walk", Description = "A longer, enriching outing for active dogs who need extra exercise.", DurationMinutes = 60, Price = 38m, IsActive = true },
            new ServiceOffering { Id = 3, Name = "Drop-in visit", Description = "Food, fresh water, playtime, medication support, and a reassuring check-in.", DurationMinutes = 30, Price = 22m, IsActive = true },
            new ServiceOffering { Id = 4, Name = "Puppy visit", Description = "A gentle potty, play, feeding, and routine-building visit for young pups.", DurationMinutes = 30, Price = 26m, IsActive = true });
    }
}
