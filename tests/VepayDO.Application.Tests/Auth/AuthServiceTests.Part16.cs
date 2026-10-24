using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Auth;

public sealed partial class AuthServiceTests
{

    private sealed class InlineTenantTransactionRunner : ITenantTransactionRunner
    {
        public List<TenantContext> Activated { get; } = [];

        public Task<TResult> RunAsync<TResult>(TenantContext context, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return work(cancellationToken).AsTask();
        }

        public Task RunAsync(TenantContext context, Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return work(cancellationToken).AsTask();
        }

        public Task<TResult> RunAsUserAsync<TResult>(Guid userId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default) =>
            RunAsync(TenantContext.ForUser(userId), work, cancellationToken);

        public Task<TResult> RunAsSystemAsync<TResult>(Guid tenantId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default) =>
            RunAsync(TenantContext.ForSystem(tenantId), work, cancellationToken);

        public Task ActivateAsync(TenantContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            Activated.Add(context);
            return Task.CompletedTask;
        }

        public Task<Guid> CreateTenantAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
