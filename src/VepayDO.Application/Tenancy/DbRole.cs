using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tenancy;

public enum DbRole
{
    Owner,
    Verifier,
    System,
    Support,
}

public static class DbRoleExtensions
{

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
