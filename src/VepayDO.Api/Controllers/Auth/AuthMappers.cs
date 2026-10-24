using VepayDO.Application.Auth;
using VepayDO.Contracts.Auth;

namespace VepayDO.Api.Controllers.Auth;

internal static class AuthMappers
{
    public static MembershipResponse ToResponse(this MembershipSummary membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new MembershipResponse(
            membership.TenantId,
            membership.TenantName,
            membership.Role,
            membership.MustChangePassword);
    }

    public static IReadOnlyList<MembershipResponse> ToResponse(this IReadOnlyList<MembershipSummary> memberships)
    {
        ArgumentNullException.ThrowIfNull(memberships);

        return memberships.Select(ToResponse).ToList();
    }

    public static AuthResponse ToResponse(this AuthResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AuthResponse(result.UserId, result.Memberships.ToResponse(), result.AccessToken);
    }

    public static SelectTenantResponse ToResponse(this SelectTenantResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SelectTenantResponse(result.AccessToken, result.Memberships.ToResponse());
    }

    public static ProfileResponse ToResponse(this ProfileView profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ProfileResponse(
            profile.Id,
            profile.Email,
            profile.Name,
            profile.PreferredLocale,
            profile.MustChangePassword);
    }

    public static MeResponse ToResponse(this MeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new MeResponse(result.User.ToResponse(), result.Memberships.ToResponse());
    }

    public static SessionResponse ToResponse(this AuthSessionView session)
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
