using VepayDO.Application.Auth;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Controllers.Auth;

/// <summary>Body of <c>POST /auth/register</c>.</summary>
public sealed record RegisterRequest(string Email, string Name, string Password, string TenantName);

/// <summary>Body of <c>POST /auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Body of <c>POST /auth/select-tenant</c>.</summary>
public sealed record SelectTenantRequest(Guid TenantId);

/// <summary>Body of <c>POST /auth/forgot-password</c>.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Body of <c>POST /auth/reset-password</c>.</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>Body of <c>POST /auth/change-password</c>.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Body of <c>PATCH /auth/me</c>.</summary>
public sealed record UpdateProfileRequest(string? PreferredLocale);

/// <summary>A single business membership shown after login or tenant selection.</summary>
public sealed record MembershipResponse(Guid TenantId, string TenantName, MemberRole Role, bool MustChangePassword)
{
    public static MembershipResponse From(MembershipSummary membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new MembershipResponse(
            membership.TenantId,
            membership.TenantName,
            membership.Role,
            membership.MustChangePassword);
    }

    public static IReadOnlyList<MembershipResponse> From(IReadOnlyList<MembershipSummary> memberships)
    {
        ArgumentNullException.ThrowIfNull(memberships);

        return memberships.Select(From).ToList();
    }
}

/// <summary>Response of <c>register</c> and <c>login</c>; the refresh token goes in a cookie.</summary>
public sealed record AuthResponse(Guid UserId, IReadOnlyList<MembershipResponse> Memberships, string AccessToken)
{
    public static AuthResponse From(AuthResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AuthResponse(result.UserId, MembershipResponse.From(result.Memberships), result.AccessToken);
    }
}

/// <summary>Response of <c>select-tenant</c>.</summary>
public sealed record SelectTenantResponse(string AccessToken, IReadOnlyList<MembershipResponse> Memberships)
{
    public static SelectTenantResponse From(SelectTenantResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SelectTenantResponse(result.AccessToken, MembershipResponse.From(result.Memberships));
    }
}

/// <summary>Response of <c>refresh</c>; the rotated refresh token goes in a cookie.</summary>
public sealed record RefreshResponse(string AccessToken);

/// <summary>Response of <c>change-password</c>; the caller keeps working with the new token.</summary>
public sealed record ChangePasswordResponse(string AccessToken);

/// <summary>Neutral response of <c>forgot-password</c>.</summary>
public sealed record ForgotPasswordResponse(string Message);

/// <summary>The caller's own profile.</summary>
public sealed record ProfileResponse(Guid Id, string Email, string Name, string PreferredLocale, bool MustChangePassword)
{
    public static ProfileResponse From(ProfileView profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ProfileResponse(
            profile.Id,
            profile.Email,
            profile.Name,
            profile.PreferredLocale,
            profile.MustChangePassword);
    }
}

/// <summary>Response of <c>GET /auth/me</c>.</summary>
public sealed record MeResponse(ProfileResponse User, IReadOnlyList<MembershipResponse> Memberships)
{
    public static MeResponse From(MeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new MeResponse(ProfileResponse.From(result.User), MembershipResponse.From(result.Memberships));
    }
}

/// <summary>One active session of the caller (UC-AUTH-06).</summary>
public sealed record SessionResponse(
    Guid Id,
    string? DeviceLabel,
    string? Ip,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    bool Current)
{
    public static SessionResponse From(AuthSessionView session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionResponse(
            session.Id,
            session.DeviceLabel,
            session.Ip?.ToString(),
            session.CreatedAt,
            session.LastSeenAt,
            session.Current);
    }
}

/// <summary>Response of <c>PATCH /auth/me</c>.</summary>
public sealed record UpdateLocaleResponse(string PreferredLocale);
