namespace VepayDO.Api.Kernel.Attributes;

/// <summary>
/// Requires the request to originate from the configured <c>AppOrigin</c>.
/// CSRF mitigation for the cookie-authenticated endpoints (refresh, logout):
/// access-token endpoints rely on the <c>Authorization</c> header and do not
/// need it (reference <c>OriginGuard</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CheckOriginAttribute : Attribute { }