using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Authorize]
[Route("api/users")]
public class UsersController(AppDbContext db, UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.Owner) ? AppRoles.Owner : AppRoles.Customer;
        return Ok(new UserProfileDto(user.Id, user.FullName, user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty, role));
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(UpdateProfileDto request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.Phone.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.Owner) ? AppRoles.Owner : AppRoles.Customer;
        return Ok(new UserProfileDto(user.Id, user.FullName, user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty, role));
    }

    [HttpGet("customers"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<IReadOnlyList<CustomerSummaryDto>>> Customers(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().Include(user => user.Dogs)
            .OrderBy(user => user.FullName).ToListAsync(cancellationToken);
        var customers = new List<CustomerSummaryDto>();
        foreach (var user in users)
        {
            if (await userManager.IsInRoleAsync(user, AppRoles.Customer))
                customers.Add(new CustomerSummaryDto(user.Id, user.FullName, user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty, user.Dogs.Select(dog => dog.ToDto()).ToList()));
        }
        return Ok(customers);
    }
}
