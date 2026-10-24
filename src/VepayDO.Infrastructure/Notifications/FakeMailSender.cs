using VepayDO.Application.Notifications;

namespace VepayDO.Infrastructure.Notifications;

public sealed class FakeMailSender : IMailSender
{
    private readonly Lock _gate = new();
    private readonly List<MailMessage> _messages = [];

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
