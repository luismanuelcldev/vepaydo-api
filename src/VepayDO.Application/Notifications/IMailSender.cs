namespace VepayDO.Application.Notifications;

public sealed record MailMessage(string To, string Subject, string TextBody);

public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
