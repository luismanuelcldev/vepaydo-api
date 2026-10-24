namespace VepayDO.Infrastructure.Persistence.Entities;

using System.Net;
using VepayDO.Domain.Enums;

public sealed class Bank
{
    public short Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public short ParserWave { get; set; }
    public bool IsActive { get; set; }
}

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

public sealed class Session
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
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

public sealed class UxEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Props { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

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
