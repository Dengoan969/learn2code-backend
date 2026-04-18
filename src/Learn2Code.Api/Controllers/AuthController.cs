using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Learn2Code.Core.DTOs;
using Learn2Code.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == request.Login);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("login", user.Login),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var keyString = _config["Jwt:Key"] ?? "super_secret_key_that_is_at_least_16_chars";
        if (keyString.Length < 16)
            throw new InvalidOperationException("JWT key must be at least 16 characters long for HS256 algorithm");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _config["Jwt:Issuer"],
            _config["Jwt:Audience"],
            claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var userDto = new UserDto(user.Id, user.Login, user.DisplayName, user.Role.ToString(), user.CreatedAt);
        return Ok(new LoginResponse(tokenString, userDto));
    }

    /// <summary>
    ///     Смена пароля текущего пользователя
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized();

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
            return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Текущий пароль неверен");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    ///     Получить информацию о текущем аутентифицированном пользователе
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized();

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
            return Unauthorized();

        var userDto = new UserDto(
            user.Id,
            user.Login,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt
        );
        return Ok(new MeResponse(userDto));
    }
}