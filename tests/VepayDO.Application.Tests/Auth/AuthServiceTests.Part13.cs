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

    private sealed class RecordingTenantFactory : ITenantFactory
    {
        public int CreateCalls { get; private set; }

        public Task<Guid> CreateAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEventDraft> Events { get; } = [];

        public Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draft);

            Events.Add(draft);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMailSender : IMailSender
    {
        public List<MailMessage> Sent { get; } = [];

        public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public static string Compute(string password) => $"hash:{password}";

        public string Hash(string plainPassword) => Compute(plainPassword);

        public bool Verify(string passwordHash, string plainPassword) =>
            string.Equals(passwordHash, Compute(plainPassword), StringComparison.Ordinal);
    }
}
