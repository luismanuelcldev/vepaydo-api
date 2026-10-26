using System.Text.Json.Serialization;
using VepayDO.Application.Auth;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Controllers.Auth;

public sealed record MembershipResponse(Guid TenantId, string TenantName, MemberRole Role, bool MustChangePassword)
{
    public static MembershipResponse From(MembershipSummary m) =>
        new(m.TenantId, m.TenantName, m.Role, m.MustChangePassword);

    public static IReadOnlyList<MembershipResponse> From(IReadOnlyList<MembershipSummary> list) =>
        list.Select(From).ToList();
}

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

    public static AuthResponse From(AuthResult result) =>
        new(result.UserId, MembershipResponse.From(result.Memberships), result.AccessToken);
}

public sealed record SelectTenantResponse(string AccessToken, IReadOnlyList<MembershipResponse> Memberships)
{
    public static SelectTenantResponse From(SelectTenantResult result) =>
        new(result.AccessToken, MembershipResponse.From(result.Memberships));
}

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

public sealed record ProfileResponse(Guid Id, string Email, string Name, string PreferredLocale, bool MustChangePassword)
{
    public static ProfileResponse From(ProfileView p) =>
        new(p.Id, p.Email, p.Name, p.PreferredLocale, p.MustChangePassword);
}

public sealed record MeResponse(ProfileResponse User, IReadOnlyList<MembershipResponse> Memberships)
{
    public static MeResponse From(MeResult result) =>
        new(ProfileResponse.From(result.User), MembershipResponse.From(result.Memberships));
}

public sealed record SessionResponse(Guid Id, string? DeviceLabel, string? Ip, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, bool Current)
{
    public static SessionResponse From(AuthSessionView s) =>
        new(s.Id, s.DeviceLabel, s.Ip?.ToString(), s.CreatedAt, s.LastSeenAt, s.Current);
}

public sealed record UpdateLocaleResponse(string PreferredLocale);
