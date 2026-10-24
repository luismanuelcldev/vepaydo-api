using System.Net;
using VepayDO.Domain.Enums;

namespace VepayDO.Application.Auth;

public sealed record RequestMeta(IPAddress? Ip, string? UserAgent, string? DeviceLabel);

public sealed record RegisterInput(string Email, string Name, string Password, string TenantName);

public sealed record LoginInput(string Email, string Password);

public sealed record MembershipSummary(
    Guid TenantId,
    string TenantName,
    MemberRole Role,
    bool MustChangePassword);

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset RefreshExpiresAt);

public sealed record AuthResult(
    Guid UserId,
    IReadOnlyList<MembershipSummary> Memberships,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);

public sealed record SelectTenantResult(string AccessToken, IReadOnlyList<MembershipSummary> Memberships);

public sealed record ProfileView(Guid Id, string Email, string Name, string PreferredLocale, bool MustChangePassword);

public sealed record MeResult(ProfileView User, IReadOnlyList<MembershipSummary> Memberships);

public sealed record AuthSessionView(
    Guid Id,
    string? DeviceLabel,
    IPAddress? Ip,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    bool Current);

public sealed record PasswordResetOptions(
    TimeSpan Lifetime,
    IPAddress? RequestedIp = null,
    Guid? IssuedBySupportId = null);
