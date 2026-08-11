using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.DTOs;

public record AvailabilityWriteDto(
    DayOfWeek? DayOfWeek,
    DateOnly? SpecificDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsAvailable,
    [MaxLength(300)] string? Notes);

public record AvailabilityDto(
    int Id, DayOfWeek? DayOfWeek, DateOnly? SpecificDate,
    TimeOnly StartTime, TimeOnly EndTime, bool IsAvailable, string Notes);

public record AvailableSlotDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);
