using System.Net;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;

namespace VepayDO.Infrastructure.Notifications;

public sealed class SmtpMailSender : IMailSender, IDisposable
{
    private readonly AppConfig _config;
    private readonly System.Net.Mail.SmtpClient _client;

    public SmtpMailSender(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _client = new System.Net.Mail.SmtpClient(config.MailHost ?? "localhost", config.MailPort);

        if (!string.IsNullOrEmpty(config.MailUser))
        {
            _client.Credentials = new NetworkCredential(config.MailUser, config.MailPassword);
        }
    }

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var mail = new System.Net.Mail.MailMessage(
            _config.MailFrom,
            message.To,
            message.Subject,
            message.TextBody);

        await _client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _client.Dispose();
}
