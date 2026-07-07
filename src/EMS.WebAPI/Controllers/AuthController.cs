using System.Security.Claims;
using EMS.Application.Common.Interfaces;
using EMS.Shared.Auth;
using EMS.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "ems_refresh";
    private const string RefreshCookiePath = "/api/v1/auth";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            // One message for wrong password / unknown user / locked / inactive:
            // never reveal which (design §9).
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.");
        }

        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresUtc);
        return result.Response;
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)
            || string.IsNullOrEmpty(refreshToken))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing refresh token.");
        }

        var result = await authService.RefreshAsync(refreshToken, cancellationToken);
        if (result is null)
        {
            DeleteRefreshCookie();
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid refresh token.");
        }

        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresUtc);
        return result.Response;
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)
            && !string.IsNullOrEmpty(refreshToken))
        {
            await authService.LogoutAsync(refreshToken, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        Name = User.Identity?.Name,
        Email = User.FindFirstValue(ClaimTypes.Email),
        Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
        Permissions = User.FindAll(Permissions.ClaimType).Select(c => c.Value).ToArray(),
    });

    private void SetRefreshCookie(string token, DateTime expiresUtc)
        => Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Secure follows the request scheme so local http development and tests work;
            // production is HTTPS-only via HSTS, making the cookie always Secure there.
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expiresUtc,
        });

    private void DeleteRefreshCookie()
        => Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
}
