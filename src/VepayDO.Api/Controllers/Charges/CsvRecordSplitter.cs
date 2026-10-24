namespace VepayDO.Api.Controllers.Charges;

internal static class CsvRecordSplitter
{
    public static List<List<string>> SplitRecords(string content)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { current.Append('"'); i++; }
                    else { inQuotes = false; }
                }
                else { current.Append(c); }
                continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': fields.Add(current.ToString()); current.Clear(); break;
                case '\r': break;
                case '\n': fields.Add(current.ToString()); current.Clear(); records.Add(fields); fields = []; break;
                default: current.Append(c); break;
            }
        }

        if (current.Length > 0 || fields.Count > 0)
        {
            fields.Add(current.ToString());
            records.Add(fields);
        }

        return records;
    }
}
