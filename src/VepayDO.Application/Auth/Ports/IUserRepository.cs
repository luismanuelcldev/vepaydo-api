namespace VepayDO.Application.Auth.Ports;

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

public sealed record NewUser(
    Guid Id,
    string Email,
    string Name,
    string PasswordHash,
    string PreferredLocale,
    bool MustChangePassword);

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
