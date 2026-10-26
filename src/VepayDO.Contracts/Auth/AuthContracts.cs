using System.Text.Json.Serialization;
using VepayDO.Domain.Enums;

namespace VepayDO.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Name, string Password, string TenantName);

public sealed record LoginRequest(string Email, string Password);

public sealed record SelectTenantRequest(Guid TenantId, string? RefreshToken = null);

/// <summary>
/// Body for <c>POST /auth/refresh</c>. Browser clients authenticate through the
/// httpOnly refresh cookie; native clients (X-Client-Type: native) send the
/// current refresh token here and receive the rotated one in the response.
/// </summary>
public sealed record RefreshRequest(string? RefreshToken);

/// <summary>
/// Body for <c>POST /auth/logout</c>. Native clients (X-Client-Type: native)
/// send their current refresh token so only the matching session is revoked;
/// browsers rely on the ambient cookie, and a missing token revokes all the
/// user's sessions.
/// </summary>
public sealed record LogoutRequest(string? RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string? PreferredLocale);

public sealed record MembershipResponse(Guid TenantId, string TenantName, MemberRole Role, bool MustChangePassword);

public sealed record AuthResponse(Guid UserId, IReadOnlyList<MembershipResponse> Memberships, string AccessToken)
{
    /// <summary>
    /// The refresh token issued by register/login, returned in the body only to
    /// native clients (X-Client-Type: native). Browser clients always receive
    /// it through the httpOnly refresh cookie and must never see it in the
    /// response body.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }
}

public sealed record SelectTenantResponse(string AccessToken, IReadOnlyList<MembershipResponse> Memberships);

public sealed record RefreshResponse(string AccessToken)
{
    /// <summary>
    /// The rotated refresh token, returned in the body only to native clients
    /// (X-Client-Type: native). Browser clients always receive it through the
    /// httpOnly refresh cookie and must never see it in the response body.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }
}

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
