namespace VepayDO.Api.Tests;

/// <summary>Stable identities used by the HTTP kernel tests.</summary>
internal static class TestData
{
    public static readonly Guid OwnerUserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");

    public static readonly Guid VerifierUserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");

    public static readonly Guid TenantId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001");
}
