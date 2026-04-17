using GoatLab.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoatLab.Server.Services.Pdf.Templates;

public class HealthCertificateDocument : IDocument
{
    private readonly Goat _goat;
    private readonly List<MedicalRecord> _vaccinations;
    private readonly WeightRecord? _latestWeight;
    private readonly FamachaScore? _latestFamacha;
    private readonly string _tenantName;

    public HealthCertificateDocument(
        Goat goat,
        List<MedicalRecord> vaccinations,
        WeightRecord? latestWeight,
        FamachaScore? latestFamacha,
        string tenantName)
    {
        _goat = goat;
        _vaccinations = vaccinations;
        _latestWeight = latestWeight;
        _latestFamacha = latestFamacha;
        _tenantName = tenantName;
    }

    public DocumentMetadata GetMetadata() => new() { Title = $"Health Certificate — {_goat.Name}" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.Letter);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text("Health Certificate").FontSize(20).SemiBold();
                col.Item().Text(_tenantName).FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().Text("This document summarizes records on file. It is not a veterinary attestation.")
                    .Italic().FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(15).Column(col =>
            {
                col.Spacing(10);

                col.Item().Text("Animal").SemiBold();
                col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                {
                    c.Spacing(2);
                    c.Item().Text(_goat.Name).FontSize(14).SemiBold();
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text(t => { t.Span("Ear tag: ").SemiBold(); t.Span(_goat.EarTag ?? "—"); });
                        r.RelativeItem().Text(t => { t.Span("Breed: ").SemiBold(); t.Span(_goat.Breed ?? "—"); });
                        r.RelativeItem().Text(t => { t.Span("Sex: ").SemiBold(); t.Span(_goat.Gender.ToString()); });
                    });
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text(t => { t.Span("DOB: ").SemiBold(); t.Span(_goat.DateOfBirth?.ToString("MMM d, yyyy") ?? "—"); });
                        r.RelativeItem().Text(t => { t.Span("Reg #: ").SemiBold(); t.Span(_goat.RegistrationNumber ?? "—"); });
                        r.RelativeItem().Text(t => { t.Span("Scrapie: ").SemiBold(); t.Span(_goat.ScrapieTag ?? "—"); });
                    });
                });

                col.Item().PaddingTop(5).Text("Current condition").SemiBold();
                col.Item().Row(r =>
                {
                    r.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Latest weight").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(_latestWeight is null
                            ? "No weight on file"
                            : $"{_latestWeight.Weight:0.#} lb on {_latestWeight.Date:MMM d, yyyy}");
                    });
                    r.ConstantItem(10);
                    r.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Latest FAMACHA").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(_latestFamacha is null
                            ? "No FAMACHA on file"
                            : $"{_latestFamacha.Score} on {_latestFamacha.Date:MMM d, yyyy}");
                    });
                });

                col.Item().PaddingTop(10).Text("Vaccinations (last 12 months)").SemiBold();
                if (_vaccinations.Count == 0)
                {
                    col.Item().Text("No vaccination records on file in the last 12 months.")
                        .Italic().FontColor(Colors.Grey.Darken1);
                }
                else
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Date").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Vaccine").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Dose").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("By").SemiBold();
                        });
                        foreach (var v in _vaccinations)
                        {
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                .Text(v.Date.ToString("MMM d, yyyy"));
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                .Text(v.Title);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                .Text(v.Dosage ?? "—");
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                                .Text(v.AdministeredBy ?? "—");
                        }
                    });
                }

                col.Item().PaddingTop(40).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                        c.Item().Text("Owner / authorized signatory").FontSize(9).FontColor(Colors.Grey.Darken1);
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
