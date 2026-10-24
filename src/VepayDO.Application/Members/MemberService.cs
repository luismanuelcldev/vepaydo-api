using System.Security.Cryptography;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Members;

public sealed partial class MemberService(
    IUserRepository users,
    IMembershipRepository memberships,
    ISessionRepository sessions,
    ISessionRevocationStore revocation,
    IPasswordHasher hasher,
    IAuditWriter audit,
    IClock clock,
    ITenantContextAccessor tenantContext) : IMemberService
{
    private const int TemporaryPasswordLength = 16;

    public async Task<IReadOnlyList<MemberView>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var records = await memberships.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return records.Select(ToView).ToList();
    }

    public async Task RevokeSessionsAsync(Guid userId, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);
        var tenantId = RequireTenantId();
        var member = await memberships.FindAsync(tenantId, userId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Member not found");

        var revoked = await RevokeAllSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("session")
                .EntityId(userId)
                .Action("revoke_member_sessions")
                .After(new { memberUserId = member.UserId, revokedCount = revoked })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var revoked = await sessions.RevokeAllForUserAsync(userId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        foreach (var sessionId in revoked)
        {
            await revocation.RevokeAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }
        return revoked.Count;
    }

    private static MemberView ToView(MembershipRecord record) =>
        new(record.UserId, record.UserName ?? string.Empty, record.UserEmail ?? string.Empty, record.Role, record.IsActive, record.CreatedAt);

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty) throw new MissingTenantContextException();
        return tenantId.Value;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateTemporaryPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TemporaryPasswordLength * 3 / 4))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
