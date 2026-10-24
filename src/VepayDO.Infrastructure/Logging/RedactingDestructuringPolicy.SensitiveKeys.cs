namespace VepayDO.Infrastructure.Logging;

public sealed partial class RedactingDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordhash", "newpassword", "currentpassword", "currentpasswordhash",
        "token", "tokenhash", "accesstoken", "refreshtoken", "refreshtokenhash",
        "passwordresettoken", "resettoken", "authorization", "cookie", "setcookie",
        "secret", "jwtaccesssecret", "refreshtokenpepper", "apikey", "sender",
        "sendername", "sendernamenormalized", "remitente", "reference",
        "referencenormalized", "last4", "destinationlast4", "accountdigits",
        "cardnumber", "email",
    };
}
