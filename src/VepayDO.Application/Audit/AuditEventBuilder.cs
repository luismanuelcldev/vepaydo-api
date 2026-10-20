using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Audit;

/// <summary>
/// Fluent builder for <see cref="AuditEventDraft"/> (AGENTS.md section 8.3).
/// Serializes the before/after snapshots to JSON so callers pass plain objects
/// and the wire format is decided in one place.
/// </summary>
public sealed class AuditEventBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private Guid _tenantId;
    private Guid? _userId;
    private AuditActor _actor = AuditActor.USER;
    private string? _entity;
    private Guid? _entityId;
    private string? _action;
    private string? _before;
    private string? _after;
    private string? _reason;
    private IPAddress? _ip;

    public AuditEventBuilder TenantId(Guid tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public AuditEventBuilder UserId(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public AuditEventBuilder Actor(AuditActor actor)
    {
        _actor = actor;
        return this;
    }

    public AuditEventBuilder Entity(string entity)
    {
        _entity = entity;
        return this;
    }

    public AuditEventBuilder EntityId(Guid? entityId)
    {
        _entityId = entityId;
        return this;
    }

    public AuditEventBuilder Action(string action)
    {
        _action = action;
        return this;
    }

    public AuditEventBuilder Before(object? snapshot)
    {
        _before = Serialize(snapshot);
        return this;
    }

    public AuditEventBuilder After(object? snapshot)
    {
        _after = Serialize(snapshot);
        return this;
    }

    public AuditEventBuilder Reason(string? reason)
    {
        _reason = reason;
        return this;
    }

    public AuditEventBuilder Ip(IPAddress? ip)
    {
        _ip = ip;
        return this;
    }

    public AuditEventDraft Build()
    {
        if (_tenantId == Guid.Empty)
        {
            throw new InvalidArgumentDomainException("An audit event requires a tenant id");
        }

        if (string.IsNullOrWhiteSpace(_entity))
        {
            throw new InvalidArgumentDomainException("An audit event requires an entity");
        }

        if (string.IsNullOrWhiteSpace(_action))
        {
            throw new InvalidArgumentDomainException("An audit event requires an action");
        }

        return new AuditEventDraft
        {
            TenantId = _tenantId,
            UserId = _userId,
            Actor = _actor,
            Entity = _entity,
            EntityId = _entityId,
            Action = _action,
            Before = _before,
            After = _after,
            Reason = _reason,
            Ip = _ip,
        };
    }

    private static string? Serialize(object? snapshot) =>
        snapshot is null ? null : JsonSerializer.Serialize(snapshot, JsonOptions);
}
