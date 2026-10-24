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

    private sealed class Harness
    {
        public Harness()
        {
            Users = new FakeUserRepository();
            Sessions = new FakeSessionRepository();
            ResetTokens = new FakePasswordResetTokenRepository();
            Memberships = new FakeMembershipRepository();
            TenantFactory = new RecordingTenantFactory();
            Audit = new RecordingAuditWriter();
            Tokens = new FakeTokenService();
            Revocation = new FakeSessionRevocationStore();
            Lockout = new InMemoryLoginAttemptStore();
            Limiter = new InMemoryPasswordResetRateLimiter();
            Mail = new RecordingMailSender();

            Service = new AuthService(
                Users,
                Sessions,
                ResetTokens,
                Memberships,
                TenantFactory,
                Audit,
                Tokens,
                new FakePasswordHasher(),
                Lockout,
                Limiter,
                Revocation,
                Mail,
                new AppConfig { AppOrigin = "http://localhost:5173", BrandName = "VepayDO" },
                new FixedClock(Now),
                new NullTenantContextAccessor(),
                Runner);
        }

        public FakeUserRepository Users { get; }

        public FakeSessionRepository Sessions { get; }

        public FakePasswordResetTokenRepository ResetTokens { get; }

        public FakeMembershipRepository Memberships { get; }

        public RecordingTenantFactory TenantFactory { get; }

        public RecordingAuditWriter Audit { get; }

        public FakeTokenService Tokens { get; }

        public FakeSessionRevocationStore Revocation { get; }

        public InMemoryLoginAttemptStore Lockout { get; }

        public InMemoryPasswordResetRateLimiter Limiter { get; }

        public RecordingMailSender Mail { get; }

        public InlineTenantTransactionRunner Runner { get; } = new();

        public AuthService Service { get; }

        public Guid NewSession(Guid userId)
        {
            var id = Guid.NewGuid();
            Sessions.Seed(
                new SessionRecord
                {
                    Id = id,
                    UserId = userId,
                    ExpiresAt = Now.AddDays(30),
                    CreatedAt = Now,
                },
                Encoding.UTF8.GetBytes(id.ToString()));

            return id;
        }
    }
}
