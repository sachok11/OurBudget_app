using Microsoft.AspNetCore.Mvc;
using FamilyBudget.Application.Services;

namespace FamilyBudget.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = await _authService.RegisterAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName);

        if (user == null)
            return BadRequest(new { message = "Email already exists" });

        var token = await _authService.LoginAsync(request.Email, request.Password);
        
        return Ok(new { 
            user = new { 
                user.Id, 
                user.Email, 
                user.FirstName, 
                user.LastName 
            }, 
            token 
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _authService.LoginAsync(request.Email, request.Password);

        if (token == null)
            return Unauthorized(new { message = "Invalid credentials" });

        var user = await _authService.GetUserByIdAsync(Guid.Parse(token));
        
        return Ok(new { token });
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName);