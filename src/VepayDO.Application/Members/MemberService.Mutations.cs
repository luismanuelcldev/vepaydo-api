using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Members;

public sealed partial class MemberService
{
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
        if (existing is not null) throw new ConflictException("Email already registered");

        var temporaryPassword = GenerateTemporaryPassword();
        var userId = Guid.NewGuid();

        await users.CreateAsync(
            new NewUser(userId, email, name, hasher.Hash(temporaryPassword), AuthConstants.DefaultLocale, MustChangePassword: true),
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
        if (newRole == current.Role && newActive == current.IsActive) return ToView(current);

        var losesOwner = current.Role == MemberRole.OWNER && current.IsActive && (newRole != MemberRole.OWNER || !newActive);
        if (losesOwner)
        {
            var activeOwners = await memberships.CountActiveOwnersAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (activeOwners <= 1) throw new ForbiddenException("The last active owner cannot be demoted or deactivated");
        }

        if (newRole != current.Role) await memberships.UpdateRoleAsync(tenantId, userId, newRole, cancellationToken).ConfigureAwait(false);
        if (newActive != current.IsActive) await memberships.SetActiveAsync(tenantId, userId, newActive, cancellationToken).ConfigureAwait(false);
        if (!newActive && current.IsActive) await RevokeAllSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

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

        return new MemberView(userId, current.UserName ?? string.Empty, current.UserEmail ?? string.Empty, newRole, newActive, current.CreatedAt);
    }
}
