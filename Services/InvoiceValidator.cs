using System.Text.Json;
using AzureFunctions.PuppeteerSharpPdf.Models;

namespace AzureFunctions.PuppeteerSharpPdf.Services;

public static class InvoiceValidator
{
    public const int MaxInvoiceNumberLength = 80;
    public const int MaxCustomerNameLength = 120;
    public const int MaxDescriptionLength = 200;
    public const int MaxNotesLength = 1000;
    public const int MaxLineItems = 100;
    public const decimal MaxQuantity = 100000m;
    public const decimal MaxUnitPrice = 10000000m;

    private static readonly HashSet<string> TopLevelFields =
        new(StringComparer.Ordinal) { "invoiceNumber", "customerName", "lineItems", "notes" };

    private static readonly HashSet<string> LineItemFields =
        new(StringComparer.Ordinal) { "description", "quantity", "unitPrice" };

    public static InvoiceValidationResult Validate(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return InvoiceValidationResult.Failure(
                [new ValidationError("$", "Request body must be a JSON object.")]);
        }

        var errors = new List<ValidationError>();
        AddUnknownFieldErrors(payload, TopLevelFields, string.Empty, errors);

        var invoiceNumber = ValidateText(
            payload,
            "invoiceNumber",
            "invoiceNumber",
            MaxInvoiceNumberLength,
            errors);
        var customerName = ValidateText(
            payload,
            "customerName",
            "customerName",
            MaxCustomerNameLength,
            errors);
        var notes = ValidateText(
            payload,
            "notes",
            "notes",
            MaxNotesLength,
            errors,
            required: false);

        var lineItems = ValidateLineItems(payload, errors);

        return errors.Count == 0
            ? InvoiceValidationResult.Success(
                new Invoice(invoiceNumber, customerName, lineItems, notes))
            : InvoiceValidationResult.Failure(errors);
    }

    private static IReadOnlyList<InvoiceLineItem> ValidateLineItems(
        JsonElement payload,
        List<ValidationError> errors)
    {
        if (!payload.TryGetProperty("lineItems", out var lineItemsElement) ||
            lineItemsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new ValidationError("lineItems", "Must be an array."));
            return Array.Empty<InvoiceLineItem>();
        }

        var itemCount = lineItemsElement.GetArrayLength();
        if (itemCount is < 1 or > MaxLineItems)
        {
            errors.Add(
                new ValidationError(
                    "lineItems",
                    $"Must contain between 1 and {MaxLineItems} items."));
            return Array.Empty<InvoiceLineItem>();
        }

        var lineItems = new List<InvoiceLineItem>(itemCount);
        var index = 0;
        foreach (var item in lineItemsElement.EnumerateArray())
        {
            var itemPath = $"lineItems[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationError(itemPath, "Must be an object."));
                index++;
                continue;
            }

            AddUnknownFieldErrors(item, LineItemFields, $"{itemPath}.", errors);
            lineItems.Add(
                new InvoiceLineItem(
                    ValidateText(
                        item,
                        "description",
                        $"{itemPath}.description",
                        MaxDescriptionLength,
                        errors),
                    ValidateNumber(
                        item,
                        "quantity",
                        $"{itemPath}.quantity",
                        decimal.Zero,
                        MaxQuantity,
                        errors,
                        minimumExclusive: true),
                    ValidateNumber(
                        item,
                        "unitPrice",
                        $"{itemPath}.unitPrice",
                        decimal.Zero,
                        MaxUnitPrice,
                        errors)));
            index++;
        }

        return lineItems;
    }

    private static void AddUnknownFieldErrors(
        JsonElement value,
        HashSet<string> allowedFields,
        string path,
        List<ValidationError> errors)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowedFields.Contains(property.Name))
            {
                errors.Add(new ValidationError($"{path}{property.Name}", "Field is not supported."));
            }
        }
    }

    private static string ValidateText(
        JsonElement parent,
        string propertyName,
        string field,
        int maxLength,
        List<ValidationError> errors,
        bool required = true)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (required)
            {
                errors.Add(new ValidationError(field, "Field is required."));
            }

            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError(field, "Must be a string."));
            return string.Empty;
        }

        var normalized = value.GetString()!.Trim();
        if (required && normalized.Length == 0)
        {
            errors.Add(new ValidationError(field, "Must not be empty."));
        }

        if (normalized.Length > maxLength)
        {
            errors.Add(
                new ValidationError(field, $"Must be {maxLength} characters or fewer."));
        }

        return normalized;
    }

    private static decimal ValidateNumber(
        JsonElement parent,
        string propertyName,
        string field,
        decimal minimum,
        decimal maximum,
        List<ValidationError> errors,
        bool minimumExclusive = false)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDecimal(out var number))
        {
            errors.Add(new ValidationError(field, "Must be a finite number."));
            return decimal.Zero;
        }

        var belowMinimum = minimumExclusive ? number <= minimum : number < minimum;
        if (belowMinimum || number > maximum)
        {
            var minimumText = minimumExclusive ? $"greater than {minimum}" : $"at least {minimum}";
            errors.Add(
                new ValidationError(
                    field,
                    $"Must be {minimumText} and no more than {maximum}."));
        }

        return number;
    }
}
