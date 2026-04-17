using GoatLab.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoatLab.Server.Services.Pdf.Templates;

public class PedigreeDocument : IDocument
{
    private readonly Goat _goat;
    private readonly string _tenantName;

    public PedigreeDocument(Goat goat, string tenantName)
    {
        _goat = goat;
        _tenantName = tenantName;
    }

    public DocumentMetadata GetMetadata() => new() { Title = $"Pedigree — {_goat.Name}" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.Letter);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text("Pedigree Certificate").FontSize(20).SemiBold();
                col.Item().Text(_tenantName).FontSize(11).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(15).Column(col =>
            {
                col.Spacing(8);

                col.Item().Text(_goat.Name).FontSize(16).SemiBold();
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Ear tag: ").SemiBold(); t.Span(_goat.EarTag ?? "—"); });
                    r.RelativeItem().Text(t => { t.Span("Breed: ").SemiBold(); t.Span(_goat.Breed ?? "—"); });
                    r.RelativeItem().Text(t => { t.Span("Sex: ").SemiBold(); t.Span(_goat.Gender.ToString()); });
                });
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("DOB: ").SemiBold(); t.Span(_goat.DateOfBirth?.ToString("MMM d, yyyy") ?? "—"); });
                    r.RelativeItem().Text(t => { t.Span("Reg #: ").SemiBold(); t.Span(_goat.RegistrationNumber ?? "—"); });
                    r.RelativeItem().Text(t => { t.Span("Registry: ").SemiBold(); t.Span(_goat.Registry.ToString()); });
                });

                col.Item().PaddingTop(15).Text("Three-generation pedigree").FontSize(13).SemiBold();

                col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c => RenderBranch(c, "Sire", _goat.Sire));
                    row.RelativeItem().Column(c => RenderBranch(c, "Dam", _goat.Dam));
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

    private static void RenderBranch(ColumnDescriptor col, string label, Goat? parent)
    {
        col.Spacing(4);
        col.Item().Text(label).SemiBold().FontColor(Colors.Green.Darken2);
        if (parent is null)
        {
            col.Item().Text("Unknown").Italic().FontColor(Colors.Grey.Medium);
            return;
        }
        col.Item().Text(parent.Name);
        if (!string.IsNullOrEmpty(parent.RegistrationNumber))
            col.Item().Text($"Reg #: {parent.RegistrationNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);

        col.Item().PaddingLeft(10).PaddingTop(4).Column(c =>
        {
            c.Spacing(4);
            c.Item().Text("Sire").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
            c.Item().Text(parent.Sire?.Name ?? "Unknown")
                .FontSize(9)
                .FontColor(parent.Sire is null ? Colors.Grey.Medium : Colors.Black);
            if (parent.Sire is { } sire)
            {
                c.Item().PaddingLeft(8).Text($"⤷ {sire.Sire?.Name ?? "?"}  /  {sire.Dam?.Name ?? "?"}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            }
            c.Item().Text("Dam").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
            c.Item().Text(parent.Dam?.Name ?? "Unknown")
                .FontSize(9)
                .FontColor(parent.Dam is null ? Colors.Grey.Medium : Colors.Black);
            if (parent.Dam is { } dam)
            {
                c.Item().PaddingLeft(8).Text($"⤷ {dam.Sire?.Name ?? "?"}  /  {dam.Dam?.Name ?? "?"}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });
    }
}
