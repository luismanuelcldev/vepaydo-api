using System.Globalization;
using VepayDO.Application.Charges;

namespace VepayDO.Api.Controllers.Charges;

internal static class ChargeCsvParser
{
    public const int MaxRows = 1000;
    private static readonly string[] ExpectedHeader = ["code", "description", "amount", "currency", "issued_on", "due_on", "customer_id"];

    public static IReadOnlyList<ChargeImportRow> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var lines = CsvRecordSplitter.SplitRecords(content);
        if (lines.Count == 0) throw new CsvFormatException(0, "The file is empty");

        ValidateHeader(lines[0]);
        var rows = new List<ChargeImportRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].Count == 1 && string.IsNullOrWhiteSpace(lines[i][0])) continue;
            if (rows.Count >= MaxRows) throw new CsvFormatException(i + 1, $"The file exceeds the maximum of {MaxRows} rows");
            rows.Add(ParseRow(lines[i], i + 1));
        }

        return rows;
    }

    private static ChargeImportRow ParseRow(List<string> fields, int rowNumber)
    {
        if (fields.Count < ExpectedHeader.Length) throw new CsvFormatException(rowNumber, "The row has fewer columns than the header");

        var code = Blank(fields[0]);
        var description = fields[1].Trim();
        var amount = ParseDecimal(fields[2], rowNumber);
        var currency = fields[3].Trim().ToUpperInvariant();
        var issuedOn = ParseDate(fields[4], rowNumber, "issued_on");
        var dueOnText = Blank(fields[5]);
        var dueOn = dueOnText is null ? (DateOnly?)null : ParseDate(dueOnText, rowNumber, "due_on");
        var customerIdText = Blank(fields[6]);
        Guid? customerId = null;
        if (customerIdText is not null)
        {
            if (!Guid.TryParse(customerIdText, out var parsed)) throw new CsvFormatException(rowNumber, "customer_id is not a valid identifier");
            customerId = parsed;
        }

        return new ChargeImportRow(rowNumber, customerId, code, description, amount, currency, issuedOn, dueOn);
    }

    private static decimal ParseDecimal(string value, int rowNumber) =>
        decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : throw new CsvFormatException(rowNumber, "amount is not a valid number");

    private static DateOnly ParseDate(string value, int rowNumber, string column) =>
        DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed : throw new CsvFormatException(rowNumber, $"{column} must use the yyyy-MM-dd format");

    private static void ValidateHeader(List<string> header)
    {
        if (header.Count < ExpectedHeader.Length) throw new CsvFormatException(1, "The header is missing columns");
        for (var i = 0; i < ExpectedHeader.Length; i++)
        {
            if (!string.Equals(header[i].Trim(), ExpectedHeader[i], StringComparison.OrdinalIgnoreCase))
                throw new CsvFormatException(1, $"The header must be: {string.Join(',', ExpectedHeader)}");
        }
    }

    private static string? Blank(string value) { var trimmed = value.Trim(); return trimmed.Length == 0 ? null : trimmed; }
}
