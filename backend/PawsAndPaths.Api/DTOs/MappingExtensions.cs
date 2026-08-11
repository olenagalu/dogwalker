using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.DTOs;

public static class MappingExtensions
{
    public static DogDto ToDto(this Dog dog) => new(
        dog.Id, dog.Name, dog.Breed, dog.Age,
        dog.CareInstructions, dog.BehavioralNotes, dog.MedicalNotes);

    public static ServiceDto ToDto(this ServiceOffering service) => new(
        service.Id, service.Name, service.Description,
        service.DurationMinutes, service.Price, service.IsActive);

    public static AvailabilityDto ToDto(this AvailabilityRule rule) => new(
        rule.Id, rule.DayOfWeek, rule.SpecificDate, rule.StartTime,
        rule.EndTime, rule.IsAvailable, rule.Notes);

    public static BookingDto ToDto(this Booking booking) => new(
        booking.Id, booking.User.FullName, booking.User.Email ?? string.Empty,
        booking.DogId, booking.Dog.Name, booking.ServiceOfferingId,
        booking.ServiceOffering.Name, booking.Date, booking.StartTime,
        booking.EndTime, booking.Price, booking.SpecialInstructions,
        booking.Status, booking.CreatedAt);
}
