namespace AzureFunctions.PuppeteerSharpPdf.Models;

public sealed record Invoice(
    string InvoiceNumber,
    string CustomerName,
    IReadOnlyList<InvoiceLineItem> LineItems,
    string? Notes);

public sealed record InvoiceLineItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record ValidationError(string Field, string Message);

public sealed record InvoiceValidationResult(
    bool IsValid,
    Invoice? Invoice,
    IReadOnlyList<ValidationError> Errors)
{
    public static InvoiceValidationResult Success(Invoice invoice) =>
        new(true, invoice, Array.Empty<ValidationError>());

    public static InvoiceValidationResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors);
}
