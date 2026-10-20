using System.Globalization;

namespace VepayDO.Application.Notifications;

/// <summary>
/// Spanish (es-DO) transactional email copy. Kept in code for the MVP; the
/// frontend owns presentational copy through i18n, but email is the one place
/// the API renders user-facing text itself (AGENTS.md sections 3.2 and 9.9).
/// </summary>
public static class MailTemplates
{
    public static MailMessage PasswordReset(string to, string resetUrl, string brandName)
    {
        ArgumentException.ThrowIfNullOrEmpty(to);
        ArgumentException.ThrowIfNullOrEmpty(resetUrl);
        ArgumentException.ThrowIfNullOrEmpty(brandName);

        var body = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            Recibimos una solicitud para restablecer la contraseña de tu cuenta de {brandName}.

            Abre este enlace para crear una contraseña nueva:
            {resetUrl}

            Si no solicitaste este cambio, ignora este correo. El enlace vence pronto y solo puede usarse una vez.
            """);

        return new MailMessage(to, $"{brandName}: restablecer contraseña", body);
    }

    public static MailMessage PasswordChanged(string to, string brandName)
    {
        ArgumentException.ThrowIfNullOrEmpty(to);
        ArgumentException.ThrowIfNullOrEmpty(brandName);

        var body = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            Tu contraseña de {brandName} fue cambiada y se cerraron las sesiones abiertas.

            Si no reconoces este cambio, escribe de inmediato a soporte.
            """);

        return new MailMessage(to, $"{brandName}: contraseña cambiada", body);
    }
}
