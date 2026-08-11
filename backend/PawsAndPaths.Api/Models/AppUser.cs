using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PawsAndPaths.Api.Models;

public class AppUser : IdentityUser
{
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    public ICollection<Dog> Dogs { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}

public static class AppRoles
{
    public const string Customer = "Customer";
    public const string Owner = "Owner";
}
