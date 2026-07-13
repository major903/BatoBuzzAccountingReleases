using BatoBuzz.Api.Security;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("authentication")]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return result.Success
            ? Ok(ApiResponse<AuthResult>.Ok(result))
            : BadRequest(ApiResponse<AuthResult>.Fail(result.Errors.ToArray()));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return result.Success
            ? Ok(ApiResponse<AuthResult>.Ok(result))
            : Unauthorized(ApiResponse<AuthResult>.Fail(result.Errors.ToArray()));
    }

    [AllowAnonymous]
    [HttpPost("login-offline")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> LoginOffline([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginOfflineAsync(request);
        return result.Success
            ? Ok(ApiResponse<AuthResult>.Ok(result))
            : Unauthorized(ApiResponse<AuthResult>.Fail(result.Errors.ToArray()));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var changed = await _authService.ChangePasswordAsync(User.GetRequiredUserId(), request);
        if (!changed)
        {
            return BadRequest(ApiResponse.Fail(
                "The current password is incorrect or the new password does not meet the requirements."));
        }

        return Ok(ApiResponse.Ok("Password changed. Sign in again with the new password."));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(User.GetRequiredUserId());
        return Ok(ApiResponse.Ok("Logged out successfully."));
    }
}
