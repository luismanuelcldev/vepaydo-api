using VepayDO.Application.Auth;
using VepayDO.Application.Tenants;

namespace VepayDO.Api.Tests.Fixtures;

public sealed class FakeTenantSettingsService : ITenantSettingsService
{
    public TenantSettingsView Current { get; set; } = new(7, 0m, null, 24);

    public Exception? Error { get; set; }

    public UpdateTenantSettingsInput? LastUpdate { get; private set; }

    public void Reset()
    {
        Current = new TenantSettingsView(7, 0m, null, 24);
        Error = null;
        LastUpdate = null;
    }

    public Task<TenantSettingsView> GetAsync(CancellationToken cancellationToken = default) =>
        Error is not null
            ? Task.FromException<TenantSettingsView>(Error)
            : Task.FromResult(Current);

    public Task<TenantSettingsView> UpdateAsync(
        UpdateTenantSettingsInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        LastUpdate = input;

        if (Error is not null)
        {
            return Task.FromException<TenantSettingsView>(Error);
        }

        Current = new TenantSettingsView(
            input.DateWindowDays ?? Current.DateWindowDays,
            input.AmountTolerance ?? Current.AmountTolerance,
            input.ClearVerifierAmountCap ? null : input.VerifierAmountCap ?? Current.VerifierAmountCap,
            input.ImageRetentionMonths ?? Current.ImageRetentionMonths);

        return Task.FromResult(Current);
    }
}
