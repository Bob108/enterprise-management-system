namespace EMS.Infrastructure.Identity;

/// <summary>
/// One-time-use rotating refresh token (design §9.1). Only the SHA-256 hash is stored;
/// tokens sharing a FamilyId descend from one login, so detected reuse revokes them all.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
