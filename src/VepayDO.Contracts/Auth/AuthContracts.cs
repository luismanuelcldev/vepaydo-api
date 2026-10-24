using VepayDO.Domain.Enums;

namespace VepayDO.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Name, string Password, string TenantName);

public sealed record LoginRequest(string Email, string Password);

public sealed record SelectTenantRequest(Guid TenantId);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string? PreferredLocale);

public sealed record MembershipResponse(Guid TenantId, string TenantName, MemberRole Role, bool MustChangePassword);

public sealed record AuthResponse(Guid UserId, IReadOnlyList<MembershipResponse> Memberships, string AccessToken);

public sealed record SelectTenantResponse(string AccessToken, IReadOnlyList<MembershipResponse> Memberships);

public sealed record RefreshResponse(string AccessToken);

public sealed record ChangePasswordResponse(string AccessToken);

public sealed record ForgotPasswordResponse(string Message);

public sealed record ProfileResponse(Guid Id, string Email, string Name, string PreferredLocale, bool MustChangePassword);

public sealed record MeResponse(ProfileResponse User, IReadOnlyList<MembershipResponse> Memberships);

public sealed record SessionResponse(
    Guid Id,
    string? DeviceLabel,
    string? Ip,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    bool Current);

public sealed record UpdateLocaleResponse(string PreferredLocale);
