namespace VepayDO.Api.Kernel.Attributes;

/// <summary>
/// Lets a route run while the caller still has to accept the current legal
/// documents (UC-AUTH-08). Applied to <c>legal/documents</c>,
/// <c>legal/accept</c>, <c>me</c>, <c>logout</c> and <c>change-password</c>, so a
/// user who must both change a temporary password and accept the documents can
/// never deadlock.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowPendingLegalAttribute : Attribute { }