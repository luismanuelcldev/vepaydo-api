namespace VepayDO.Api.Controllers.Auth;

public sealed record RegisterRequest(string Email, string Name, string Password, string TenantName);
public sealed record LoginRequest(string Email, string Password);
public sealed record SelectTenantRequest(Guid TenantId);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record UpdateProfileRequest(string? PreferredLocale);
