using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EMS.Application.Common.Interfaces;
using EMS.Infrastructure.Persistence;
using EMS.Shared.Auth;
using EMS.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EMS.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    EmsDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IDateTime clock) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResult?> LoginAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            // Increments the failure count and locks the account after the configured
            // maximum (5 failures / 15 minutes, design §9.1).
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // A fresh login starts a new refresh-token family.
        return await IssueTokensAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthResult?> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(refreshToken);
        var stored = await dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            return null;
        }

        var now = clock.UtcNow;

        // A revoked token being presented again means the token was stolen or replayed:
        // revoke every descendant of the same login (design §9.1).
        if (stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now || !stored.User.IsActive)
        {
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            return null;
        }

        stored.RevokedAtUtc = now; // one-time use
        await dbContext.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(stored.User, stored.FamilyId, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(refreshToken);
        var stored = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, clock.UtcNow, cancellationToken);
        }
    }

    private async Task<AuthResult> IssueTokensAsync(
        ApplicationUser user, Guid familyId, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(roles);

        var now = clock.UtcNow;
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: credentials));

        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            accessToken,
            accessExpires,
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToList(),
            permissions);

        return new AuthResult(response, refreshToken, refreshExpires);
    }

    private async Task<IReadOnlyList<string>> GetPermissionsAsync(IEnumerable<string> roleNames)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleName in roleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var claims = await roleManager.GetClaimsAsync(role);
            foreach (var claim in claims.Where(c => c.Type == Permissions.ClaimType))
            {
                permissions.Add(claim.Value);
            }
        }

        return permissions.Order().ToList();
    }

    private Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
        => dbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
