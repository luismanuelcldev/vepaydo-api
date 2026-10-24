using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(TxProvider tx, IClock clock) : IUserRepository
{
    public async Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await tx.DbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken).ConfigureAwait(false);
        return user is null ? null : ToRecord(user);
    }

    public async Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await tx.DbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        return user is null ? null : ToRecord(user);
    }

    public async Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        tx.DbContext.Users.Add(new User
        {
            Id = user.Id, Email = user.Email, Name = user.Name, PasswordHash = user.PasswordHash,
            IsActive = true, PreferredLocale = user.PreferredLocale, MustChangePassword = user.MustChangePassword,
            IsPlatformSupport = false, CreatedAt = clock.UtcNow,
        });
        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default) =>
        tx.DbContext.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(
            s => s.SetProperty(u => u.PasswordHash, passwordHash).SetProperty(u => u.MustChangePassword, mustChangePassword), cancellationToken);

    public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default) =>
        tx.DbContext.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.PreferredLocale, preferredLocale), cancellationToken);

    public Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        tx.DbContext.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, at), cancellationToken);

    public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
        tx.DbContext.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive), cancellationToken);

    public Task SetPlatformSupportAsync(Guid userId, bool isPlatformSupport, CancellationToken cancellationToken = default) =>
        tx.DbContext.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsPlatformSupport, isPlatformSupport), cancellationToken);

    private static UserRecord ToRecord(User user) => new()
    {
        Id = user.Id, Email = user.Email, Name = user.Name, PasswordHash = user.PasswordHash,
        IsActive = user.IsActive, PreferredLocale = user.PreferredLocale, MustChangePassword = user.MustChangePassword,
        IsPlatformSupport = user.IsPlatformSupport,
    };
}
