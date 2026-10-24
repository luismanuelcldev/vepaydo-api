using System.Security.Cryptography;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Members;

/// <summary>
/// Member management use cases (UC-SET-03, UC-AUTH-09). Members are created as
/// global users with a one-time temporary password (returned once, hashed at
/// rest, forced change on first login) and attached to the caller's tenant. Every
/// mutation is audited in the same transaction.
/// </summary>
public sealed class MemberService(
    IUserRepository users,
    IMembershipRepository memberships,
    ISessionRepository sessions,
    ISessionRevocationStore revocation,
    IPasswordHasher hasher,
    IAuditWriter audit,
    IClock clock,
    ITenantContextAccessor tenantContext) : IMemberService
{
    /// <summary>Length of the generated temporary password; above the 12-char minimum.</summary>
    private const int TemporaryPasswordLength = 16;

    public async Task<IReadOnlyList<MemberView>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var records = await memberships.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return records.Select(ToView).ToList();
    }

    public async Task<CreatedMemberResult> CreateAsync(
        CreateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var email = NormalizeEmail(input.Email);
        var name = input.Name.Trim();

        var existing = await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new ConflictException("Email already registered");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var userId = Guid.NewGuid();

        await users.CreateAsync(
            new NewUser(
                userId,
                email,
                name,
                hasher.Hash(temporaryPassword),
                AuthConstants.DefaultLocale,
                MustChangePassword: true),
            cancellationToken).ConfigureAwait(false);

        await memberships.AddAsync(tenantId, userId, input.Role, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("membership")
                .EntityId(userId)
                .Action("create")
                .After(new { userId, email, name, role = input.Role.ToString() })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        var member = new MemberView(userId, name, email, input.Role, IsActive: true, CreatedAt: clock.UtcNow);

        return new CreatedMemberResult(member, temporaryPassword);
    }

    public async Task<MemberView> UpdateAsync(
        Guid userId,
        UpdateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var current = await memberships.FindAsync(tenantId, userId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Member not found");

        var newRole = input.Role ?? current.Role;
        var newActive = input.IsActive ?? current.IsActive;

        if (newRole == current.Role && newActive == current.IsActive)
        {
            return ToView(current);
        }

        // Last-active-owner guard: the tenant must always keep one active owner.
        var losesOwner = current.Role == MemberRole.OWNER && current.IsActive
                         && (newRole != MemberRole.OWNER || !newActive);
        if (losesOwner)
        {
            var activeOwners = await memberships.CountActiveOwnersAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (activeOwners <= 1)
            {
                throw new ForbiddenException("The last active owner cannot be demoted or deactivated");
            }
        }

        if (newRole != current.Role)
        {
            await memberships.UpdateRoleAsync(tenantId, userId, newRole, cancellationToken).ConfigureAwait(false);
        }

        if (newActive != current.IsActive)
        {
            await memberships.SetActiveAsync(tenantId, userId, newActive, cancellationToken).ConfigureAwait(false);
        }

        // A deactivated member must lose access immediately (UC-SET-03, UC-AUTH-07).
        if (!newActive && current.IsActive)
        {
            await RevokeAllSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("membership")
                .EntityId(userId)
                .Action("update")
                .Before(new { role = current.Role.ToString(), isActive = current.IsActive })
                .After(new { role = newRole.ToString(), isActive = newActive })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        return new MemberView(
            userId,
            current.UserName ?? string.Empty,
            current.UserEmail ?? string.Empty,
            newRole,
            newActive,
            current.CreatedAt);
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

    private static MemberView ToView(MembershipRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new MemberView(
            record.UserId,
            record.UserName ?? string.Empty,
            record.UserEmail ?? string.Empty,
            record.Role,
            record.IsActive,
            record.CreatedAt);
    }

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        return tenantId.Value;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateTemporaryPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TemporaryPasswordLength * 3 / 4))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}