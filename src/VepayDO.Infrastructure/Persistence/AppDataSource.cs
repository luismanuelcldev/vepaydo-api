using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;
using VepayDO.Domain.Enums;

namespace VepayDO.Infrastructure.Persistence;

public static class AppDataSource
{
    private static readonly NpgsqlNullNameTranslator NameTranslator = new();

    public static NpgsqlDataSourceBuilder CreateBuilder(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        builder.MapEnum<MemberRole>("member_role", NameTranslator);
        builder.MapEnum<ProcessingStatus>("processing_status", NameTranslator);
        builder.MapEnum<ReconciliationStatus>("reconciliation_status", NameTranslator);
        builder.MapEnum<AlertSeverity>("alert_severity", NameTranslator);
        builder.MapEnum<AlertStatus>("alert_status", NameTranslator);
        builder.MapEnum<AlertResolution>("alert_resolution", NameTranslator);
        builder.MapEnum<ChargeStatus>("charge_status", NameTranslator);
        builder.MapEnum<ReceiptSource>("receipt_source", NameTranslator);
        builder.MapEnum<TransferType>("transfer_type", NameTranslator);
        builder.MapEnum<TenantPlan>("tenant_plan", NameTranslator);
        builder.MapEnum<AlertRuleCode>("alert_rule_code", NameTranslator);
        builder.MapEnum<FailureReason>("failure_reason", NameTranslator);
        builder.MapEnum<RejectReasonCode>("reject_reason_code", NameTranslator);
        builder.MapEnum<LegalDocumentType>("legal_document_type", NameTranslator);
        builder.MapEnum<AuditActor>("audit_actor", NameTranslator);

        return builder;
    }

    public static NpgsqlDataSource Build(string connectionString)
    {
        return CreateBuilder(connectionString).Build();
    }

    public static void ConfigureEnums(NpgsqlDbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.MapEnum<MemberRole>(enumName: "member_role", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<ProcessingStatus>(enumName: "processing_status", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<ReconciliationStatus>(enumName: "reconciliation_status", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<AlertSeverity>(enumName: "alert_severity", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<AlertStatus>(enumName: "alert_status", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<AlertResolution>(enumName: "alert_resolution", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<ChargeStatus>(enumName: "charge_status", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<ReceiptSource>(enumName: "receipt_source", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<TransferType>(enumName: "transfer_type", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<TenantPlan>(enumName: "tenant_plan", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<AlertRuleCode>(enumName: "alert_rule_code", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<FailureReason>(enumName: "failure_reason", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<RejectReasonCode>(enumName: "reject_reason_code", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<LegalDocumentType>(enumName: "legal_document_type", schemaName: null, nameTranslator: NameTranslator);
        options.MapEnum<AuditActor>(enumName: "audit_actor", schemaName: null, nameTranslator: NameTranslator);
    }
}
