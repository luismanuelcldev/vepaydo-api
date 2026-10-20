using VepayDO.Application.Notifications;

namespace VepayDO.Infrastructure.Notifications;

/// <summary>
/// In-memory <see cref="IMailSender"/> for tests and local development. Mirrors
/// the reference <c>FakeMailSender</c>: it records every message so assertions
/// can inspect the reset link without a real SMTP server (AGENTS.md section 9.9).
/// </summary>
public sealed class FakeMailSender : IMailSender
{
    private readonly Lock _gate = new();
    private readonly List<MailMessage> _messages = [];

    /// <summary>A snapshot copy of every message sent so far.</summary>
    public IReadOnlyList<MailMessage> Sent
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToList();
            }
        }
    }

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
        }
    }
}
