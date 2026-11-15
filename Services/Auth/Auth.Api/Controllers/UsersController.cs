using Auth.Api.Constants;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Extensions;
using Auth.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var list = await users.Users
            .OrderBy(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var result = new List<UserDto>();
        foreach (var u in list) result.Add(await ToDtoAsync(u));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();

        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await ToDtoAsync(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(string id, UpdateUserRequest request)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();

        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? Ok(await ToDtoAsync(user)) : this.IdentityProblem(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == User.GetUserId())
            return Problem(statusCode: 409, title: "You cannot delete your own account");

        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var result = await users.DeleteAsync(user);
        return result.Succeeded ? NoContent() : this.IdentityProblem(result);
    }

    [HttpGet("{id}/refresh-tokens")]
    public async Task<ActionResult<IReadOnlyList<RefreshTokenDto>>> GetRefreshTokens(string id, CancellationToken ct)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();
        if (await users.FindByIdAsync(id) is null) return NotFound();

        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        // ’еш≥ токен≥в назовн≥ не в≥ддаЇмо
        return Ok(tokens.Select(t => new RefreshTokenDto(
            t.Id, t.CreatedAtUtc, t.ExpiresAtUtc, t.RevokedAtUtc, t.IsActive)));
    }

    [HttpGet("{id}/roles")]
    public async Task<ActionResult<IList<string>>> GetRoles(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await users.GetRolesAsync(user));
    }

    [HttpPost("{id}/roles")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> AssignRole(string id, AssignRoleRequest request)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await roles.RoleExistsAsync(request.Role))
            return Problem(statusCode: 400, title: $"Role '{request.Role}' does not exist");

        var result = await users.AddToRoleAsync(user, request.Role);
        return result.Succeeded ? NoContent() : this.IdentityProblem(result);
    }

    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var result = await users.RemoveFromRoleAsync(user, role);
        return result.Succeeded ? NoContent() : this.IdentityProblem(result);
    }

    // Owner-перев≥рка: userId з токена зб≥гаЇтьс€ з {id} або користувач Ч Admin
    private bool IsOwnerOrAdmin(string id)
        => User.IsInRole(AppRoles.Admin) || User.GetUserId() == id;

    private async Task<UserDto> ToDtoAsync(ApplicationUser u)
        => new(u.Id, u.UserName!, u.Email!, u.FullName, u.EmailConfirmed,
               await users.GetRolesAsync(u), u.CreatedAtUtc);
}