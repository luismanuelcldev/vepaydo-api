namespace VepayDO.Application.Notifications;

/// <summary>A plain-text transactional email (AGENTS.md section 9.9).</summary>
public sealed record MailMessage(string To, string Subject, string TextBody);

/// <summary>
/// Sends transactional email behind a port so tests use <c>FakeMailSender</c>
/// and development uses Mailpit over SMTP. Templates come from
/// <see cref="MailTemplates"/> (Spanish copy, AGENTS.md section 3.2).
/// </summary>
public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
