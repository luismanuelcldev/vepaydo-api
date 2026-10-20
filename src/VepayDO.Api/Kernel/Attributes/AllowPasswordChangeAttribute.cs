namespace VepayDO.Api.Kernel.Attributes;

/// <summary>
/// Lets a route run while the caller still carries a one-time temporary password
/// (forced change on first login). Applied to <c>change-password</c>,
/// <c>logout</c> and <c>me</c>, so a user who must change the password can still
/// reach the routes that let them do it (reference <c>AuthorizationGuard</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowPasswordChangeAttribute : Attribute { }