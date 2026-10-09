using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using AzureFunctions.PuppeteerSharpPdf.Models;

namespace AzureFunctions.PuppeteerSharpPdf.Services;

public static class InvoiceHtmlBuilder
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    public static string Build(Invoice invoice, DateTimeOffset? generatedAt = null)
    {
        var generatedDate = (generatedAt ?? DateTimeOffset.UtcNow).UtcDateTime;
        var total = invoice.LineItems.Sum(item => item.Quantity * item.UnitPrice);
        var rows = new StringBuilder();

        foreach (var item in invoice.LineItems)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            rows.Append(
                $"""
                        <tr>
                            <td>{Encode(item.Description)}</td>
                            <td class="number">{item.Quantity.ToString("0.###", UsCulture)}</td>
                            <td class="number">{item.UnitPrice.ToString("C", UsCulture)}</td>
                            <td class="number">{lineTotal.ToString("C", UsCulture)}</td>
                        </tr>
                """);
        }

        var notes = string.IsNullOrEmpty(invoice.Notes)
            ? string.Empty
            : $"""
                    <section class="notes" aria-labelledby="notes-heading">
                        <h2 id="notes-heading">Notes</h2>
                        <p>{Encode(invoice.Notes)}</p>
                    </section>
              """;

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Invoice {{Encode(invoice.InvoiceNumber)}}</title>
                <style>
                    :root {
                        color: #172033;
                        font-family: Arial, "Liberation Sans", "Noto Sans", sans-serif;
                        font-size: 12px;
                    }
                    * { box-sizing: border-box; }
                    body { margin: 0; background: #fff; color: #172033; }
                    main { width: 100%; padding: 8mm 6mm; }
                    header {
                        display: flex;
                        align-items: flex-start;
                        justify-content: space-between;
                        gap: 24px;
                        border-bottom: 3px solid #2563eb;
                        padding-bottom: 18px;
                        margin-bottom: 26px;
                    }
                    h1 {
                        margin: 0;
                        color: #1d4ed8;
                        font-size: 34px;
                        letter-spacing: 0.08em;
                        text-transform: uppercase;
                    }
                    h2 { margin: 0 0 8px; font-size: 15px; }
                    p { margin: 0; line-height: 1.5; }
                    .meta {
                        min-width: 230px;
                        border-left: 1px solid #cbd5e1;
                        padding-left: 20px;
                    }
                    .meta-row {
                        display: grid;
                        grid-template-columns: 90px 1fr;
                        gap: 10px;
                        padding: 3px 0;
                    }
                    .label { color: #475569; font-weight: 700; }
                    .bill-to {
                        background: #eff6ff;
                        border-left: 4px solid #3b82f6;
                        border-radius: 4px;
                        padding: 15px 18px;
                        margin-bottom: 24px;
                    }
                    table { width: 100%; border-collapse: collapse; margin-bottom: 18px; }
                    caption {
                        position: absolute;
                        width: 1px;
                        height: 1px;
                        padding: 0;
                        margin: -1px;
                        overflow: hidden;
                        clip: rect(0, 0, 0, 0);
                        white-space: nowrap;
                        border: 0;
                    }
                    th {
                        background: #1e3a8a;
                        color: #fff;
                        font-size: 11px;
                        letter-spacing: 0.04em;
                        padding: 11px 10px;
                        text-align: left;
                        text-transform: uppercase;
                    }
                    td {
                        border-bottom: 1px solid #dbeafe;
                        padding: 11px 10px;
                        vertical-align: top;
                    }
                    tbody tr:nth-child(even) { background: #f8fafc; }
                    .number { text-align: right; white-space: nowrap; }
                    .total { display: flex; justify-content: flex-end; margin-bottom: 28px; }
                    .total-box {
                        display: grid;
                        grid-template-columns: auto auto;
                        gap: 22px;
                        min-width: 280px;
                        background: #172554;
                        color: #fff;
                        border-radius: 4px;
                        padding: 14px 18px;
                        font-size: 17px;
                        font-weight: 700;
                    }
                    .notes { border-top: 1px solid #cbd5e1; padding-top: 16px; }
                    .notes p { white-space: pre-wrap; overflow-wrap: anywhere; }
                    footer {
                        margin-top: 34px;
                        color: #64748b;
                        font-size: 10px;
                        text-align: center;
                    }
                    @page { size: A4; margin: 12mm; }
                </style>
            </head>
            <body>
                <main>
                    <header>
                        <div>
                            <h1>Invoice</h1>
                            <p>Generated by an Azure Functions custom-container demo.</p>
                        </div>
                        <div class="meta" aria-label="Invoice details">
                            <div class="meta-row"><span class="label">Invoice</span><span>{{Encode(invoice.InvoiceNumber)}}</span></div>
                            <div class="meta-row"><span class="label">Generated</span><span>{{generatedDate.ToString("MMMM d, yyyy", UsCulture)}} UTC</span></div>
                        </div>
                    </header>

                    <section class="bill-to" aria-labelledby="bill-to-heading">
                        <h2 id="bill-to-heading">Bill to</h2>
                        <p>{{Encode(invoice.CustomerName)}}</p>
                    </section>

                    <table>
                        <caption>Invoice line items</caption>
                        <thead>
                            <tr>
                                <th scope="col">Description</th>
                                <th scope="col" class="number">Quantity</th>
                                <th scope="col" class="number">Unit price</th>
                                <th scope="col" class="number">Amount</th>
                            </tr>
                        </thead>
                        <tbody>
            {{rows}}
                        </tbody>
                    </table>

                    <section class="total" aria-label="Invoice total">
                        <div class="total-box">
                            <span>Total (USD)</span>
                            <span>{{total.ToString("C", UsCulture)}}</span>
                        </div>
                    </section>

            {{notes}}

                    <footer>This sample assumes USD and stores no invoice data.</footer>
                </main>
            </body>
            </html>
            """;
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
