namespace VepayDO.Application.Auth.Ports;

/// <summary>A user row as the application layer needs it (never the ORM entity).</summary>
public sealed record UserRecord
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string Name { get; init; }

    public required string PasswordHash { get; init; }

    public required bool IsActive { get; init; }

    public required string PreferredLocale { get; init; }

    public required bool MustChangePassword { get; init; }

    public required bool IsPlatformSupport { get; init; }
}

/// <summary>Data required to create a user.</summary>
public sealed record NewUser(
    Guid Id,
    string Email,
    string Name,
    string PasswordHash,
    string PreferredLocale,
    bool MustChangePassword);

/// <summary>
/// Reads and writes <c>users</c>. The table has no RLS, so it is reachable
/// before the caller is trusted, exactly like the reference API (AGENTS.md section 9.9).
/// </summary>
public interface IUserRepository
{
    Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task CreateAsync(NewUser user, CancellationToken cancellationToken = default);

    Task UpdatePasswordAsync(
        Guid userId,
        string passwordHash,
        bool mustChangePassword,
        CancellationToken cancellationToken = default);

    Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default);

    Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    Task SetPlatformSupportAsync(Guid userId, bool isPlatformSupport, CancellationToken cancellationToken = default);
}
