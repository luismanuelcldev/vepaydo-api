using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Security;

public static class Permissions
{
    public const string ReceiptsReview = "receipts.review";

    public const string ReceiptsDecide = "receipts.decide";

    public const string ReceiptsRevert = "receipts.revert";

    public const string AlertsResolveCritical = "alerts.resolveCritical";

    public const string ChargesManage = "charges.manage";

    public const string CustomersManage = "customers.manage";

    public const string CustomersAliasMove = "customers.aliasMove";

    public const string BusinessAccountsManage = "businessAccounts.manage";

    public const string MembersManage = "members.manage";

    public const string SettingsManage = "settings.manage";

    public const string AuditRead = "audit.read";

    public const string ReconciliationView = "reconciliation.view";

    public static readonly IReadOnlyList<string> All =
    [
        ReceiptsReview,
        ReceiptsDecide,
        ReceiptsRevert,
        AlertsResolveCritical,
        ChargesManage,
        CustomersManage,
        CustomersAliasMove,
        BusinessAccountsManage,
        MembersManage,
        SettingsManage,
        AuditRead,
        ReconciliationView,
    ];
}

public static class PermissionPolicy
{
    private static readonly IReadOnlySet<string> OwnerPermissions = new HashSet<string>(
        Permissions.All,
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> VerifierPermissions = new HashSet<string>(
        [
            Permissions.ReceiptsReview,
            Permissions.ReceiptsDecide,
            Permissions.ChargesManage,
            Permissions.CustomersManage,
        ],
        StringComparer.Ordinal);

    public static bool Can(MemberRole? role, string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        IReadOnlySet<string>? granted = role switch
        {
            MemberRole.OWNER => OwnerPermissions,
            MemberRole.VERIFIER => VerifierPermissions,
            _ => null,
        };

        return granted is not null && granted.Contains(permission);
    }

    public static void Assert(MemberRole? role, string permission)
    {
        if (!Can(role, permission))
        {
            throw new ForbiddenException($"Missing permission: {permission}");
        }
    }
}
