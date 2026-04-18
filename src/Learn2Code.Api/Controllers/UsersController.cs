using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    ///     Получить список всех пользователей
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _db.Users
            .OrderBy(u => u.Login)
            .ToListAsync();

        var dtos = users.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    ///     Получить пользователя по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        return Ok(MapToDto(user));
    }

    /// <summary>
    ///     Создать нового пользователя (только администратор)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Login == request.Login);
        if (existing != null)
            return Conflict("Пользователь с таким логином уже существует");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return BadRequest("Недопустимая роль. Допустимые значения: Student, Teacher, Admin");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = request.Login,
            DisplayName = request.DisplayName,
            Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapToDto(user));
    }

    /// <summary>
    ///     Обновить данные пользователя
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        if (!string.IsNullOrEmpty(request.Login) && request.Login != user.Login)
        {
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Login == request.Login);
            if (existing != null)
                return Conflict("Пользователь с таким логином уже существует");
            user.Login = request.Login;
        }

        if (!string.IsNullOrEmpty(request.DisplayName))
            user.DisplayName = request.DisplayName;

        if (!string.IsNullOrEmpty(request.Role))
        {
            if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
                return BadRequest("Недопустимая роль. Допустимые значения: Student, Teacher, Admin");
            user.Role = role;
        }

        await _db.SaveChangesAsync();
        return Ok(MapToDto(user));
    }

    /// <summary>
    ///     Удалить пользователя
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    ///     Сбросить пароль пользователя (администратор)
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto(
            user.Id,
            user.Login,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt
        );
    }
}