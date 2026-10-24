namespace VepayDO.Api.Controllers.Charges;

internal sealed class CsvFormatException(int rowNumber, string message) : Exception(message)
{
    public int RowNumber { get; } = rowNumber;
}
