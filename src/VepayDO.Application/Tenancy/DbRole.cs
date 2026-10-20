using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tenancy;

/// <summary>
/// Role exposed to the database as <c>app.role</c> and read back by
/// <c>app.current_member_role()</c> (AGENTS.md sections 4.2 and 7).
/// <c>system</c> is used by background jobs and automatic re-evaluation;
/// <c>support</c> by platform support sessions (section 9.9).
/// </summary>
public enum DbRole
{
    Owner,
    Verifier,
    System,
    Support,
}

public static class DbRoleExtensions
{
    /// <summary>Canonical lowercase value stored in the <c>app.role</c> setting.</summary>
    public static string ToDatabaseValue(this DbRole role) => role switch
    {
        DbRole.Owner => "owner",
        DbRole.Verifier => "verifier",
        DbRole.System => "system",
        DbRole.Support => "support",
        _ => throw new InvalidArgumentDomainException($"Unknown database role: {role}"),
    };

    public static DbRole FromDatabaseValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            "owner" => DbRole.Owner,
            "verifier" => DbRole.Verifier,
            "system" => DbRole.System,
            "support" => DbRole.Support,
            _ => throw new InvalidArgumentDomainException($"Unknown database role: {value}"),
        };
    }

    public static DbRole ToDbRole(this MemberRole role) => role switch
    {
        MemberRole.OWNER => DbRole.Owner,
        MemberRole.VERIFIER => DbRole.Verifier,
        _ => throw new InvalidArgumentDomainException($"Unknown member role: {role}"),
    };
}
