namespace VepayDO.Api.Kernel.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowPendingLegalAttribute : Attribute { }
