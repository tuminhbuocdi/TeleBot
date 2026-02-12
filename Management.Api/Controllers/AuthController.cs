using Microsoft.AspNetCore.Mvc;
using Management.Application.Auth;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwt;

    public AuthController(JwtService jwt)
    {
        _jwt = jwt;
    }

    [HttpPost("login")]
    public IActionResult Login()
    {
        var token = _jwt.Generate(1, "admin");
        return Ok(token);
    }
}
