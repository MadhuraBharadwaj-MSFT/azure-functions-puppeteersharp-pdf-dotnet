using System.Globalization;
using System.Text;

namespace AzureFunctions.PuppeteerSharpPdf.Services;

public static class InvoiceFilename
{
    public static string Create(string invoiceNumber)
    {
        var normalized = invoiceNumber.Normalize(NormalizationForm.FormKD);
        var safePart = new StringBuilder(normalized.Length);
        var lastWasSeparator = false;

        foreach (var character in normalized)
        {
            if (character > 127 ||
                CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            {
                safePart.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                safePart.Append('-');
                lastWasSeparator = true;
            }

            if (safePart.Length >= 60)
            {
                break;
            }
        }

        var value = safePart.ToString().Trim('.', '-');
        return $"invoice-{(value.Length == 0 ? "document" : value)}.pdf";
    }
}
