using System.Globalization;
using System.Text;

namespace VepayDO.Domain.Text;

/// <summary>
/// Pure, case-, accent- and punctuation-insensitive normalization used for
/// customer names, aliases and the matching engine (AGENTS.md section 9.3).
/// </summary>
public static class TextNormalizer
{
    public static string NormalizeName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(' ');
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
