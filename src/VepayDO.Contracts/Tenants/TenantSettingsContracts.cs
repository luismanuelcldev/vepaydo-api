namespace VepayDO.Contracts.Tenants;

/// <summary>
/// Wire contracts for the tenant settings module (UC-SET-04). The shape is the
/// JSON the API exposes and the frontend consumes; it carries no mapping logic.
/// </summary>
public sealed record TenantSettingsResponse(
    short DateWindowDays,
    decimal AmountTolerance,
    decimal? VerifierAmountCap,
    short ImageRetentionMonths);

/// <summary>
/// Body of <c>PATCH /settings</c>. Every field is optional (a partial update);
/// <see cref="ClearVerifierAmountCap"/> removes the cap without leaving it
/// ambiguous with an absent field.
/// </summary>
public sealed record UpdateSettingsRequest(
    short? DateWindowDays,
    decimal? AmountTolerance,
    decimal? VerifierAmountCap,
    bool? ClearVerifierAmountCap,
    short? ImageRetentionMonths);