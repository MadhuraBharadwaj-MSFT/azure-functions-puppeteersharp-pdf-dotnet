using System.Text.Json;
using AzureFunctions.PuppeteerSharpPdf.Services;

namespace AzureFunctions.PuppeteerSharpPdf.Tests;

public sealed class InvoiceTests
{
    private const string ValidInvoiceJson =
        """
        {
          "invoiceNumber": "INV-2026-0042",
          "customerName": "Contoso Coffee",
          "lineItems": [
            { "description": "Accessibility review", "quantity": 2, "unitPrice": 125 },
            { "description": "PDF hosting", "quantity": 1, "unitPrice": 49.5 }
          ],
          "notes": "Thank you for your business."
        }
        """;

    [Fact]
    public void Validate_normalizes_a_valid_invoice()
    {
        using var document = JsonDocument.Parse(ValidInvoiceJson);

        var result = InvoiceValidator.Validate(document.RootElement);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Invoice);
        Assert.Equal("INV-2026-0042", result.Invoice.InvoiceNumber);
        Assert.Equal("Contoso Coffee", result.Invoice.CustomerName);
        Assert.Equal(2, result.Invoice.LineItems.Count);
        Assert.Equal(299.5m, result.Invoice.LineItems.Sum(
            item => item.Quantity * item.UnitPrice));
    }

    [Fact]
    public void Validate_reports_field_errors_and_rejects_unstructured_input()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "invoiceNumber": "",
              "customerName": 42,
              "lineItems": [
                {
                  "description": "Item",
                  "quantity": 0,
                  "unitPrice": -1,
                  "url": "https://example.com"
                }
              ],
              "rawHtml": "<script>alert(1)</script>"
            }
            """);

        var result = InvoiceValidator.Validate(document.RootElement);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == "invoiceNumber");
        Assert.Contains(result.Errors, error => error.Field == "customerName");
        Assert.Contains(result.Errors, error => error.Field == "lineItems[0].quantity");
        Assert.Contains(result.Errors, error => error.Field == "lineItems[0].unitPrice");
        Assert.Contains(result.Errors, error => error.Field == "lineItems[0].url");
        Assert.Contains(result.Errors, error => error.Field == "rawHtml");
    }

    [Fact]
    public void Build_escapes_content_and_includes_accessible_table_semantics()
    {
        using var document = JsonDocument.Parse(
            ValidInvoiceJson
                .Replace("Contoso Coffee", "<img src=x onerror=alert(1)>")
                .Replace(
                    "Thank you for your business.",
                    "Use <strong>plain text</strong> only."));
        var invoice = InvoiceValidator.Validate(document.RootElement).Invoice!;

        var html = InvoiceHtmlBuilder.Build(
            invoice,
            new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero));

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.Contains("&lt;strong&gt;plain text&lt;/strong&gt;", html);
        Assert.Contains("<caption>Invoice line items</caption>", html);
        Assert.Contains("<th scope=\"col\">Description</th>", html);
        Assert.Contains("$299.50", html);
        Assert.Contains("@page { size: A4; margin: 12mm; }", html);
    }

    [Fact]
    public void Create_filename_removes_unsafe_characters()
    {
        var filename = InvoiceFilename.Create("../../Invoice 42\r\n\"unsafe\"");

        Assert.Equal("invoice-Invoice-42-unsafe.pdf", filename);
    }
}
