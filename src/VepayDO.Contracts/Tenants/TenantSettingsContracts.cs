namespace VepayDO.Contracts.Tenants;

public sealed record TenantSettingsResponse(
    short DateWindowDays,
    decimal AmountTolerance,
    decimal? VerifierAmountCap,
    short ImageRetentionMonths);

public sealed record UpdateSettingsRequest(
    short? DateWindowDays,
    decimal? AmountTolerance,
    decimal? VerifierAmountCap,
    bool? ClearVerifierAmountCap,
    short? ImageRetentionMonths);
