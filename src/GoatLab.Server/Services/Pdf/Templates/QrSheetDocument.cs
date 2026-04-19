using GoatLab.Shared.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoatLab.Server.Services.Pdf.Templates;

// Printable QR sheet — one card per goat, grid laid out 2×4 on Letter. Each card
// shows the QR code (links to /herd/{id} on this deployment's origin), goat
// name, ear tag, DOB and breed. Cut along the borders, laminate, zip-tie to
// the pen or stick on the animal's tag for scan-to-lookup in the barn.
public sealed record QrSheetRow(int GoatId, string Name, string? EarTag, string? Breed, DateTime? DateOfBirth, string TargetUrl);

public class QrSheetDocument : IDocument
{
    private const int CardsPerPage = 8;

    private readonly IReadOnlyList<QrSheetRow> _rows;
    private readonly string _tenantName;

    public QrSheetDocument(IReadOnlyList<QrSheetRow> rows, string tenantName)
    {
        _rows = rows;
        _tenantName = tenantName;
    }

    public DocumentMetadata GetMetadata() => new() { Title = $"QR ear-tag sheet — {_tenantName}" };

    public void Compose(IDocumentContainer container)
    {
        var pages = _rows
            .Select((r, i) => new { r, i })
            .GroupBy(x => x.i / CardsPerPage)
            .Select(g => g.Select(x => x.r).ToList())
            .ToList();

        foreach (var pageRows in pages)
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.Letter);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().PaddingBottom(10).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Herd QR tags — ").FontSize(14).SemiBold();
                        t.Span(_tenantName).FontSize(14);
                    });
                    r.ConstantItem(140).AlignRight().Text(DateTime.UtcNow.ToString("MMM d, yyyy"))
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                    for (var i = 0; i < pageRows.Count; i++)
                    {
                        var row = pageRows[i];
                        table.Cell().Padding(6).Element(cell => RenderCard(cell, row));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Scan to open each goat's profile — ").FontColor(Colors.Grey.Medium);
                    t.Span("GoatLab").FontColor(Colors.Grey.Medium).SemiBold();
                });
            });
        }
    }

    private static void RenderCard(IContainer cell, QrSheetRow row)
    {
        cell.Border(0.75f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(10)
            .Row(card =>
            {
                card.ConstantItem(120).AlignCenter().AlignMiddle().Image(RenderQr(row.TargetUrl));
                card.RelativeItem().PaddingLeft(12).Column(col =>
                {
                    col.Spacing(3);
                    col.Item().Text(row.Name).FontSize(13).SemiBold();
                    if (!string.IsNullOrEmpty(row.EarTag))
                        col.Item().Text(t => { t.Span("Tag: ").SemiBold(); t.Span(row.EarTag); });
                    if (!string.IsNullOrEmpty(row.Breed))
                        col.Item().Text(t => { t.Span("Breed: ").SemiBold(); t.Span(row.Breed); });
                    if (row.DateOfBirth is { } dob)
                        col.Item().Text(t => { t.Span("DOB: ").SemiBold(); t.Span(dob.ToString("MMM d, yyyy")); });
                    col.Item().PaddingTop(4).Text(row.TargetUrl)
                        .FontSize(7)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
    }

    private static byte[] RenderQr(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(10);
    }
}
