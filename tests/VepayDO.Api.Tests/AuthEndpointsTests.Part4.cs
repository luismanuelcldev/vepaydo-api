using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

public sealed partial class AuthEndpointsTests
{

    [Fact]
    public async Task Change_password_maps_a_wrong_password_to_401()
    {
        _factory.Auth.ChangePasswordError = new UnauthorizedException("The current password is incorrect");
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await PostJsonAsync(client, $"{AuthBase}/change-password", new
        {
            currentPassword = "wrongpassword1",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_is_neutral_and_always_200()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/forgot-password", new
        {
            email = "someone@example.test",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("someone@example.test", _factory.Auth.LastForgotEmail);
    }

    [Fact]
    public async Task Reset_password_returns_204()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/reset-password", new
        {
            token = "a-reset-token",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("a-reset-token", _factory.Auth.LastResetToken);
    }

    [Fact]
    public async Task Reset_password_maps_an_invalid_token_to_401()
    {
        _factory.Auth.ResetPasswordError = new UnauthorizedException("Invalid or expired reset token");
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/reset-password", new
        {
            token = "stale",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
