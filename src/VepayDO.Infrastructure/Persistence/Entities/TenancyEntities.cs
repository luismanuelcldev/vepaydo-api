using System.Net;
using VepayDO.Domain.Enums;

namespace VepayDO.Infrastructure.Persistence.Entities;

/// <summary>Bank catalog (global, seeded by migration). Maps to <c>banks</c>.</summary>
public sealed class Bank
{
    public short Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public short ParserWave { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Global user account. Maps to <c>users</c>.</summary>
public sealed class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string PreferredLocale { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    public bool IsPlatformSupport { get; set; }
}

/// <summary>Refresh-token session. Maps to <c>sessions</c>.</summary>
public sealed class Session
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The tenant this session selected, if any (<c>sessions.tenant_id</c>).</summary>
    public Guid? TenantId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? DeviceLabel { get; set; }

    public IPAddress? Ip { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}

/// <summary>Password-reset token (hash only). Maps to <c>password_reset_tokens</c>.</summary>
public sealed class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public Guid? IssuedBySupportId { get; set; }

    public IPAddress? RequestedIp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Immutable legal acceptance record. Maps to <c>legal_acceptances</c>.</summary>
public sealed class LegalAcceptance
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public LegalDocumentType DocumentType { get; set; }

    public string Version { get; set; } = null!;

    public DateTimeOffset AcceptedAt { get; set; }

    public IPAddress? Ip { get; set; }

    public string? UserAgent { get; set; }
}

/// <summary>First-party UX metric event. Maps to <c>ux_events</c>.</summary>
public sealed class UxEvent
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Props { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Time-boxed platform support grant. Maps to <c>support_grants</c>.</summary>
public sealed class SupportGrant
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid GrantedBy { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Business tenant. Maps to <c>tenants</c>.</summary>
public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Rnc { get; set; }

    public string TimeZone { get; set; } = null!;

    public TenantPlan Plan { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Per-tenant configuration. Maps to <c>tenant_settings</c>.</summary>
public sealed class TenantSettings
{
    public Guid TenantId { get; set; }

    public short DateWindowDays { get; set; }

    public decimal AmountTolerance { get; set; }

    public decimal? VerifierAmountCap { get; set; }

    public short ImageRetentionMonths { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>User-to-tenant membership. Maps to <c>memberships</c>.</summary>
public sealed class Membership
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public MemberRole Role { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>FCM push device registration. Maps to <c>push_devices</c>.</summary>
public sealed class PushDevice
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Provider { get; set; } = null!;

    public string Token { get; set; } = null!;

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsed { get; set; }
}
