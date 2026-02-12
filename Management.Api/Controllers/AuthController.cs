using Microsoft.AspNetCore.Mvc;
using Management.Application.Auth;
using Management.Domain.Entities;
using Management.Infrastructure.Repositories;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwt;
    private readonly UserRepository _users;
    private readonly PasswordHasher _hasher;

    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthController(JwtService jwt, UserRepository users, PasswordHasher hasher)
    {
        _jwt = jwt;
        _users = users;
        _hasher = hasher;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest("Username and Password are required.");
        }

        var existingByUsername = await _users.GetByUsername(req.Username);
        if (existingByUsername != null)
        {
            return Conflict("Username already exists.");
        }

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var existingByEmail = await _users.GetByEmail(req.Email);
            if (existingByEmail != null)
            {
                return Conflict("Email already exists.");
            }
        }

        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            var existingByPhone = await _users.GetByPhone(req.Phone);
            if (existingByPhone != null)
            {
                return Conflict("Phone already exists.");
            }
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = req.Username.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            PasswordHash = _hasher.Hash(req.Password),
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim(),
            AvatarUrl = null,
            IsActive = true,
            IsLocked = false,
            IsDeleted = false,
            UserRole = "user",
            LastLoginAt = null,
            LastLoginIp = null,
            EmailVerified = false,
            PhoneVerified = false,
            FailedLoginCount = 0,
            LockoutEnd = null,
            CreatedAt = now,
            UpdatedAt = null
        };

        await _users.Insert(user);

        var token = _jwt.Generate(user.UserId, user.Username, user.UserRole);
        return Ok(new AuthResponse { Token = token });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Login) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest("Login and Password are required.");
        }

        var user = await _users.GetByLogin(req.Login.Trim());
        if (user == null)
        {
            return Unauthorized("Invalid credentials.");
        }

        if (!user.IsActive || user.IsDeleted)
        {
            return Unauthorized("User is inactive.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            return Unauthorized("User is locked.");
        }

        if (!_hasher.Verify(req.Password, user.PasswordHash))
        {
            var now = DateTime.UtcNow;
            var failed = user.FailedLoginCount + 1;
            DateTime? lockoutEnd = null;
            if (failed >= LockoutThreshold)
            {
                lockoutEnd = now.Add(LockoutDuration);
            }

            await _users.UpdateLoginFailure(user.UserId, failed, lockoutEnd, now);
            return Unauthorized("Invalid credentials.");
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _users.UpdateLoginSuccess(user.UserId, DateTime.UtcNow, ip);

        var token = _jwt.Generate(user.UserId, user.Username, user.UserRole);
        return Ok(new AuthResponse { Token = token });
    }
}
