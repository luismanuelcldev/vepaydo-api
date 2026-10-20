using System.Net;
using VepayDO.Domain.Enums;

namespace VepayDO.Application.Auth;

/// <summary>Request metadata captured from the HTTP boundary for auditing and sessions.</summary>
public sealed record RequestMeta(IPAddress? Ip, string? UserAgent, string? DeviceLabel);

public sealed record RegisterInput(string Email, string Name, string Password, string TenantName);

public sealed record LoginInput(string Email, string Password);

/// <summary>A business the caller belongs to, as shown by the tenant selector.</summary>
public sealed record MembershipSummary(
    Guid TenantId,
    string TenantName,
    MemberRole Role,
    bool MustChangePassword);

/// <summary>The token pair issued by register/login/refresh.</summary>
public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset RefreshExpiresAt);

/// <summary>Result of register/login: the tokens plus the caller's memberships.</summary>
public sealed record AuthResult(
    Guid UserId,
    IReadOnlyList<MembershipSummary> Memberships,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);

public sealed record SelectTenantResult(string AccessToken, IReadOnlyList<MembershipSummary> Memberships);

public sealed record ProfileView(Guid Id, string Email, string Name, string PreferredLocale, bool MustChangePassword);

public sealed record MeResult(ProfileView User, IReadOnlyList<MembershipSummary> Memberships);

/// <summary>A device session shown in the profile (UC-AUTH-06).</summary>
public sealed record AuthSessionView(
    Guid Id,
    string? DeviceLabel,
    IPAddress? Ip,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    bool Current);

/// <summary>Options for issuing a password-reset token (UC-AUTH-04/UC-AUTH-05).</summary>
public sealed record PasswordResetOptions(
    TimeSpan Lifetime,
    IPAddress? RequestedIp = null,
    Guid? IssuedBySupportId = null);
