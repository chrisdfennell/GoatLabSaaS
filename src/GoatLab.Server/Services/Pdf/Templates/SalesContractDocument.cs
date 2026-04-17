using GoatLab.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoatLab.Server.Services.Pdf.Templates;

public class SalesContractDocument : IDocument
{
    private readonly Sale _sale;
    private readonly string _tenantName;

    public SalesContractDocument(Sale sale, string tenantName)
    {
        _sale = sale;
        _tenantName = tenantName;
    }

    public DocumentMetadata GetMetadata() => new() { Title = $"Sales Contract — #{_sale.Id}" };

    public void Compose(IDocumentContainer container)
    {
        var balance = _sale.Amount - _sale.DepositAmount;

        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.Letter);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text("Sales Contract").FontSize(20).SemiBold();
                col.Item().Text(_tenantName).FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().Text($"Contract #{_sale.Id}  •  {_sale.SaleDate:MMMM d, yyyy}")
                    .FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(15).Column(col =>
            {
                col.Spacing(10);

                col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                {
                    c.Spacing(2);
                    c.Item().Text("Buyer").SemiBold();
                    c.Item().Text(_sale.Customer?.Name ?? "—");
                    if (!string.IsNullOrEmpty(_sale.Customer?.Email))
                        c.Item().Text(_sale.Customer.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(_sale.Customer?.Phone))
                        c.Item().Text(_sale.Customer.Phone).FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(_sale.Customer?.Address))
                        c.Item().Text(_sale.Customer.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                col.Item().Text("Item").SemiBold();
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Type").SemiBold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Description").SemiBold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Amount").SemiBold();
                    });
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                        .Text(_sale.SaleType.ToString());
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                        .Text(string.IsNullOrEmpty(_sale.Description)
                            ? (_sale.Goat?.Name ?? "—")
                            : _sale.Description);
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                        .AlignRight().Text($"${_sale.Amount:0.00}");
                });

                col.Item().AlignRight().Column(c =>
                {
                    c.Item().Text($"Deposit: ${_sale.DepositAmount:0.00}").FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"Balance due: ${balance:0.00}").SemiBold();
                    c.Item().Text($"Status: {_sale.PaymentStatus}").FontColor(Colors.Grey.Darken1);
                });

                if (!string.IsNullOrEmpty(_sale.Notes))
                {
                    col.Item().PaddingTop(10).Text("Notes").SemiBold();
                    col.Item().Text(_sale.Notes);
                }

                col.Item().PaddingTop(20).Text("Terms").SemiBold();
                col.Item().Text(
                    "Buyer accepts the animal/product described above as-is. " +
                    "Seller represents that to the best of their knowledge the animal is healthy at the time of sale. " +
                    "Balance is due as agreed. Signatures below acknowledge acceptance of these terms.");

                col.Item().PaddingTop(40).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                        c.Item().Text("Seller signature").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    r.ConstantItem(20);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                        c.Item().Text("Buyer signature").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Generated ").FontColor(Colors.Grey.Medium);
                t.Span(DateTime.UtcNow.ToString("MMM d, yyyy")).FontColor(Colors.Grey.Medium);
                t.Span(" — GoatLab").FontColor(Colors.Grey.Medium);
            });
        });
    }
}
