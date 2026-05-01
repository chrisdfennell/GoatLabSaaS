using GoatLab.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoatLab.Server.Services.Pdf.Templates;

// Branded three-generation pedigree certificate — printable sales tool.
// Header is a green band with the farm name; hero block shows the goat's
// primary photo (when available) next to its identity; ancestry tree is
// rendered as a fixed grid of cards instead of nested columns so deeper
// generations don't overflow into each other.
//
// Verification URL in the footer points at the goat's public listing
// (when public profile is on) so a buyer holding the printout can scan
// or type the URL and see the same record live.
public class PedigreeDocument : IDocument
{
    private readonly Goat _goat;
    private readonly string _tenantName;
    private readonly byte[]? _primaryPhoto;
    private readonly string? _verificationUrl;

    private const string AccentDark = "#1B5E20";
    private const string AccentMid = "#2E7D32";
    private const string AccentLight = "#E8F5E9";

    public PedigreeDocument(
        Goat goat,
        string tenantName,
        byte[]? primaryPhoto = null,
        string? verificationUrl = null)
    {
        _goat = goat;
        _tenantName = tenantName;
        _primaryPhoto = primaryPhoto;
        _verificationUrl = verificationUrl;
    }

    public DocumentMetadata GetMetadata() => new() { Title = $"Pedigree Certificate — {_goat.Name}" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(0);
            page.Size(PageSizes.Letter);
            page.DefaultTextStyle(t => t.FontSize(10));
            page.PageColor(Colors.White);

            // Decorative outer border keeps the "certificate" feel even when
            // printed at small scale on cheap paper.
            page.Content().Padding(18).Border(2).BorderColor(AccentMid).Padding(2).Border(0.5f).BorderColor(AccentMid)
                .Padding(20).Column(col =>
            {
                col.Spacing(14);

                // ---- Header band ----
                col.Item().Background(AccentDark).Padding(16).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("PEDIGREE CERTIFICATE")
                            .FontSize(11).LetterSpacing(0.2f).FontColor(Colors.White).SemiBold();
                        c.Item().Text(_tenantName)
                            .FontSize(20).FontColor(Colors.White).SemiBold();
                    });
                    row.ConstantItem(120).AlignRight().AlignMiddle().Text(t =>
                    {
                        t.Span("GoatLab\n").FontColor(Colors.White).FontSize(11).SemiBold();
                        t.Span(DateTime.UtcNow.ToString("MMM d, yyyy")).FontColor(Colors.White).FontSize(9);
                    });
                });

                // ---- Hero (photo + identity) ----
                col.Item().PaddingTop(6).Row(row =>
                {
                    if (_primaryPhoto is not null)
                    {
                        row.ConstantItem(160).Height(160).Background(AccentLight)
                            .Border(1).BorderColor(AccentMid)
                            .AlignCenter().AlignMiddle()
                            .Image(_primaryPhoto).FitArea();
                    }
                    else
                    {
                        row.ConstantItem(160).Height(160).Background(AccentLight)
                            .Border(1).BorderColor(AccentMid)
                            .AlignCenter().AlignMiddle()
                            .Text("🐐").FontSize(48);
                    }
                    row.ConstantItem(20);
                    row.RelativeItem().Column(c =>
                    {
                        c.Spacing(4);
                        c.Item().Text(_goat.Name).FontSize(26).SemiBold().FontColor(AccentDark);
                        if (!string.IsNullOrEmpty(_goat.Breed))
                            c.Item().Text($"{_goat.Breed} · {_goat.Gender}").FontSize(12).FontColor(Colors.Grey.Darken2);
                        else
                            c.Item().Text(_goat.Gender.ToString()).FontSize(12).FontColor(Colors.Grey.Darken2);

                        c.Item().PaddingTop(8).Element(IdentityGrid);
                    });
                });

                // ---- Three-generation ancestry ----
                col.Item().PaddingTop(8).Text("THREE-GENERATION ANCESTRY")
                    .FontSize(10).LetterSpacing(0.15f).SemiBold().FontColor(AccentDark);

                col.Item().Element(BuildPedigreeGrid);

                // ---- Footer ----
                col.Item().PaddingTop(10).BorderTop(0.5f).BorderColor(AccentMid).PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(t =>
                        {
                            t.Span("Issued by ").FontColor(Colors.Grey.Darken1).FontSize(9);
                            t.Span(_tenantName).FontColor(Colors.Grey.Darken3).FontSize(9).SemiBold();
                            t.Span($" · Generated {DateTime.UtcNow:MMM d, yyyy}").FontColor(Colors.Grey.Darken1).FontSize(9);
                        });
                        if (!string.IsNullOrEmpty(_verificationUrl))
                        {
                            c.Item().Text(t =>
                            {
                                t.Span("Verify online: ").FontColor(Colors.Grey.Darken1).FontSize(9);
                                t.Span(_verificationUrl!).FontColor(AccentDark).FontSize(9);
                            });
                        }
                    });
                    row.ConstantItem(80).AlignRight().AlignMiddle().Text("GoatLab")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        });
    }

    private void IdentityGrid(QuestPDF.Infrastructure.IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            col.Item().Row(r =>
            {
                r.RelativeItem().Element(c => Field(c, "Ear tag", _goat.EarTag));
                r.RelativeItem().Element(c => Field(c, "Date of birth", _goat.DateOfBirth?.ToString("MMM d, yyyy")));
            });
            col.Item().Row(r =>
            {
                r.RelativeItem().Element(c => Field(c, "Registration #", _goat.RegistrationNumber));
                r.RelativeItem().Element(c => Field(c, "Registry", _goat.Registry == GoatRegistry.None ? null : _goat.Registry.ToString()));
            });
            if (!string.IsNullOrEmpty(_goat.TattooLeft) || !string.IsNullOrEmpty(_goat.TattooRight) || !string.IsNullOrEmpty(_goat.Microchip))
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Element(c => Field(c, "Tattoo (L/R)",
                        $"{_goat.TattooLeft ?? "—"} / {_goat.TattooRight ?? "—"}"));
                    r.RelativeItem().Element(c => Field(c, "Microchip", _goat.Microchip));
                });
            }
            if (!string.IsNullOrEmpty(_goat.BreederName))
            {
                col.Item().Element(c => Field(c, "Breeder", _goat.BreederName));
            }
        });
    }

    private static void Field(QuestPDF.Infrastructure.IContainer container, string label, string? value)
    {
        container.Text(t =>
        {
            t.Span($"{label}: ").FontSize(9).FontColor(Colors.Grey.Darken1).SemiBold();
            t.Span(string.IsNullOrEmpty(value) ? "—" : value).FontSize(10);
        });
    }

    // Renders a compact, fixed grid of ancestor cards. Three columns roughly
    // map to "parent / grandparent / great-grandparent" so a buyer can scan
    // sire-side and dam-side bloodlines at a glance.
    private void BuildPedigreeGrid(QuestPDF.Infrastructure.IContainer container)
    {
        container.Border(1).BorderColor(AccentMid).Padding(8).Column(col =>
        {
            col.Spacing(6);
            col.Item().Element(c => RenderTriple(c, "SIRE", _goat.Sire));
            col.Item().Element(c => RenderTriple(c, "DAM", _goat.Dam));
        });
    }

    private static void RenderTriple(QuestPDF.Infrastructure.IContainer container, string topLabel, Goat? parent)
    {
        container.Row(row =>
        {
            row.RelativeItem(2).Element(c => AncestorCard(c, topLabel, parent, large: true));
            row.ConstantItem(6);
            row.RelativeItem(2).Column(col =>
            {
                col.Spacing(4);
                col.Item().Element(c => AncestorCard(c, "Sire", parent?.Sire, large: false));
                col.Item().Element(c => AncestorCard(c, "Dam", parent?.Dam, large: false));
            });
            row.ConstantItem(6);
            row.RelativeItem(3).Column(col =>
            {
                col.Spacing(2);
                col.Item().Element(c => AncestorCard(c, "Sire of Sire", parent?.Sire?.Sire, large: false, mini: true));
                col.Item().Element(c => AncestorCard(c, "Dam of Sire", parent?.Sire?.Dam, large: false, mini: true));
                col.Item().Element(c => AncestorCard(c, "Sire of Dam", parent?.Dam?.Sire, large: false, mini: true));
                col.Item().Element(c => AncestorCard(c, "Dam of Dam", parent?.Dam?.Dam, large: false, mini: true));
            });
        });
    }

    private static void AncestorCard(QuestPDF.Infrastructure.IContainer container, string label, Goat? g, bool large, bool mini = false)
    {
        var nameSize = large ? 12 : (mini ? 8 : 10);
        var labelSize = mini ? 6 : 7;
        var regSize = mini ? 6 : 7;
        string bg = large ? AccentLight : Colors.Grey.Lighten4;

        container.Background(bg).Padding(mini ? 4 : 6).Column(col =>
        {
            col.Item().Text(label.ToUpperInvariant()).FontSize(labelSize)
                .LetterSpacing(0.15f).SemiBold().FontColor(AccentDark);
            if (g is null)
            {
                col.Item().Text("Unknown").FontSize(nameSize).Italic().FontColor(Colors.Grey.Medium);
                return;
            }
            col.Item().Text(g.Name).FontSize(nameSize).SemiBold();
            if (!string.IsNullOrEmpty(g.RegistrationNumber))
                col.Item().Text($"#{g.RegistrationNumber}").FontSize(regSize).FontColor(Colors.Grey.Darken1);
            else if (!string.IsNullOrEmpty(g.EarTag))
                col.Item().Text($"Tag {g.EarTag}").FontSize(regSize).FontColor(Colors.Grey.Darken1);
        });
    }
}
