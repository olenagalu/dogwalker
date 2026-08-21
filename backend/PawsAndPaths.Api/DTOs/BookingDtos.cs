using System.ComponentModel.DataAnnotations;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.DTOs;

public record CreateBookingDto(
    [Range(1, int.MaxValue)] int DogId,
    [Range(1, int.MaxValue)] int ServiceId,
    DateOnly Date,
    TimeOnly StartTime,
    [MaxLength(2000)] string? SpecialInstructions,
    DateOnly? EndDate = null,
    TimeOnly? OvernightStartTime = null,
    TimeOnly? OvernightEndTime = null,
    TimeOnly? MiddayStartTime = null,
    TimeOnly? MiddayEndTime = null);

public record CreateOwnerBookingDto(
    [Required] string CustomerId,
    [Range(1, int.MaxValue)] int DogId,
    [Range(1, int.MaxValue)] int ServiceId,
    DateOnly Date,
    TimeOnly StartTime,
    [MaxLength(2000)] string? SpecialInstructions,
    DateOnly? EndDate = null,
    TimeOnly? OvernightStartTime = null,
    TimeOnly? OvernightEndTime = null,
    TimeOnly? MiddayStartTime = null,
    TimeOnly? MiddayEndTime = null);

public record UpdateOvernightScheduleDto(
    TimeOnly OvernightStartTime,
    TimeOnly OvernightEndTime,
    TimeOnly MiddayStartTime,
    TimeOnly MiddayEndTime);

public record UpdateBookingStatusDto(BookingStatus Status);

public record BookingDto(
    int Id, string CustomerName, string CustomerEmail, int DogId, string DogName,
    int ServiceId, string ServiceName, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    decimal Price, string SpecialInstructions, BookingStatus Status, DateTimeOffset CreatedAt,
    DateOnly? EndDate, bool IsOvernightStay, TimeOnly? OvernightStartTime,
    TimeOnly? OvernightEndTime, TimeOnly? MiddayStartTime, TimeOnly? MiddayEndTime);
