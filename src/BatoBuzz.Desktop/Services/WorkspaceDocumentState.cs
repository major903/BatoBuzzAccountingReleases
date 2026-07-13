using System.Globalization;

namespace BatoBuzz.Desktop.Services;

/// <summary>
/// Exposes state the application shell needs before a document workspace can be closed.
/// </summary>
public interface IWorkspaceDocumentState
{
    string WorkspaceName { get; }
    bool IsBusy { get; }
    bool HasUnsavedChanges { get; }
}

public static class DocumentInput
{
    private const NumberStyles DecimalStyles = NumberStyles.Number;

    public static bool TryParseDecimal(string? text, out decimal value)
    {
        var input = text?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            value = 0;
            return false;
        }

        return decimal.TryParse(input, DecimalStyles, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(input, DecimalStyles, CultureInfo.InvariantCulture, out value);
    }

    public static decimal DecimalOrZero(string? text) =>
        TryParseDecimal(text, out var value) ? value : 0m;

    public static bool TryParseDate(string? text, out DateTime value)
    {
        var input = text?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            value = default;
            return false;
        }

        if (DateTime.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value))
        {
            value = value.Date;
            return true;
        }

        value = default;
        return false;
    }

    public static string FormatDate(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);
}
