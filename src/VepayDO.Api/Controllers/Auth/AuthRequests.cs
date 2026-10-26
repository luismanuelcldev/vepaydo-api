namespace VepayDO.Api.Controllers.Auth;

public sealed record RegisterRequest(string Email, string Name, string Password, string TenantName);
public sealed record LoginRequest(string Email, string Password);
public sealed record SelectTenantRequest(Guid TenantId, string? RefreshToken = null);

/// <summary>
/// Body for <c>POST /auth/refresh</c>. Browser clients authenticate through the
/// httpOnly refresh cookie; native clients (X-Client-Type: native) send the
/// current refresh token here and receive the rotated one in the response.
/// </summary>
public sealed record RefreshRequest(string? RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record UpdateProfileRequest(string? PreferredLocale);
