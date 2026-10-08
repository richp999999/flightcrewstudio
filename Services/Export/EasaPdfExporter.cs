using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FlightCrewStudio.Services.Export;

/// <summary>Options for the EASA logbook PDF.</summary>
public sealed record EasaPdfOptions(string PilotName, string? LicenceNumber, int RowsPerPage, bool IncludeCoverPages,
    DateTime PrintedAtUtc);

/// <summary>
/// Prints the logbook in the EASA Part-FCL (AMC1 FCL.050) layout: A4 landscape, the twelve
/// numbered column groups, a fixed number of lines per page, and "This Page / Total From
/// Previous Pages / Total Time" totals with a certification line at the foot of each page.
/// </summary>
public static class EasaPdfExporter
{
    private const float FontSize = 7f;
    private const float RowHeight = 15f;
    private static readonly string Line = "#5b5b5b";
    private static readonly string HeadBg = "#eeeeee";
    private static readonly string Accent = "#0b3d91";

    // Column widths (relative), left to right:
    //  0 Date | 1 Dep place | 2 Dep time | 3 Arr place | 4 Arr time | 5 Model | 6 Reg'n |
    //  7 SE | 8 ME | 9 Multi-pilot | 10 Total | 11 Name PIC | 12 Ldg day | 13 Ldg night |
    // 14 Night | 15 IFR | 16 PIC | 17 Co-pilot | 18 Dual | 19 Instructor | 20 FSTD device | 21 FSTD time | 22 Remarks
    private static readonly float[] Widths = [68, 50, 36, 50, 36, 50, 52, 33, 33, 36, 36, 92, 30, 34, 36, 36, 36, 36, 34, 36, 38, 34, 172];

    static EasaPdfExporter()
    {
        // QuestPDF is free under its Community licence for individuals and small organisations.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(IReadOnlyList<LogbookEntry> entries, LogbookTotals broughtForward, EasaPdfOptions o)
    {
        var rows = Math.Clamp(o.RowsPerPage, 5, 25);
        var pages = entries.Chunk(rows).ToList();
        if (pages.Count == 0) pages.Add([]);

        var doc = Document.Create(container =>
        {
            if (o.IncludeCoverPages)
            {
                container.Page(page => { Setup(page, o); page.Content().Element(c => CoverPage(c, o)); });
                container.Page(page =>
                {
                    Setup(page, o);
                    page.Header().Element(c => Header(c, o, null, null));
                    page.Content().Element(LicencesPage);
                });
            }

            var carried = broughtForward;
            var recordNo = 1;
            for (var i = 0; i < pages.Count; i++)
            {
                var chunk = pages[i];
                var thisPage = LogbookTotals.Of(chunk);
                var previous = carried;
                var first = recordNo;
                var last = recordNo + chunk.Length - 1;
                var pageNo = i + 1;
                container.Page(page =>
                {
                    Setup(page, o);
                    page.Header().Element(c => Header(c, o, chunk.Length == 0 ? null : $"Records {first} to {last}", pageNo));
                    page.Content().PaddingTop(12).Column(col =>
                    {
                        col.Item().Element(TableHeader);
                        col.Item().PaddingTop(5).Element(c => TableBody(c, chunk, rows));
                        col.Item().PaddingTop(5).Element(c => TotalsBlock(c, thisPage, previous, o.PilotName));
                    });
                });
                carried = previous + thisPage;
                recordNo += chunk.Length;
            }
        });

        return doc.GeneratePdf();
    }

    // ---- page furniture -------------------------------------------------------------------

    private static void Setup(PageDescriptor page, EasaPdfOptions o)
    {
        page.Size(PageSizes.A4.Landscape());
        page.MarginHorizontal(20);
        page.MarginTop(14);
        page.MarginBottom(14);
        page.DefaultTextStyle(t => t.FontSize(FontSize).FontColor("#111111"));
        page.Footer().BorderTop(0.5f).BorderColor("#bbbbbb").PaddingTop(5).Row(r =>
        {
            r.RelativeItem().Text($"Printed on {o.PrintedAtUtc.ToString("MMMM d, yyyy 'at' HH:mm", CultureInfo.InvariantCulture)} UTC").FontSize(6.5f);
            r.RelativeItem().AlignRight().Text(t =>
            {
                t.Span("Produced by ").FontSize(6.5f);
                t.Span("FlightCrewStudio").FontSize(6.5f).FontColor("#2f5aa2");
            });
        });
    }

    private static void Header(IContainer c, EasaPdfOptions o, string? records, int? pageNo)
    {
        c.BorderBottom(0.75f).BorderColor("#111111").PaddingBottom(4).Row(r =>
        {
            r.RelativeItem(1).AlignBottom().Text("Pilot's Logbook").FontSize(14);
            r.RelativeItem(1).AlignBottom().AlignCenter().Text(PilotTitle(o)).FontSize(14);
            r.RelativeItem(1).AlignBottom().AlignRight().Row(rr =>
            {
                if (records is not null)
                    rr.AutoItem().AlignBottom().PaddingRight(8).PaddingBottom(2).Text(records).FontSize(7);
                if (pageNo is { } n)
                {
                    rr.AutoItem().AlignBottom().PaddingRight(6).Text("Page:").FontSize(14);
                    rr.AutoItem().AlignBottom().MinWidth(28).Background(Accent).PaddingHorizontal(5).PaddingVertical(1)
                        .AlignCenter().Text(n.ToString(CultureInfo.InvariantCulture)).FontSize(14).FontColor(Colors.White);
                }
            });
        });
    }

    private static string PilotTitle(EasaPdfOptions o) =>
        string.IsNullOrWhiteSpace(o.LicenceNumber) ? o.PilotName : $"{o.PilotName}   ({o.LicenceNumber})";

    private static void CoverPage(IContainer c, EasaPdfOptions o)
    {
        c.PaddingTop(80).AlignCenter().Width(480).Column(col =>
        {
            col.Spacing(6);
            col.Item().AlignCenter().Text("Pilot Logbook").FontSize(32);
            col.Item().PaddingTop(16).AlignCenter().Text(o.PilotName).FontSize(24);
            col.Item().PaddingTop(16).AlignCenter().Text("Address:").FontSize(10);
            for (var i = 0; i < 3; i++) col.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor("#333333");
            col.Item().PaddingTop(10).AlignCenter().Text("Telephone:").FontSize(10);
            col.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor("#333333");
            col.Item().PaddingTop(10).AlignCenter().Text("Licence Number:").FontSize(10);
            col.Item().PaddingTop(4).AlignCenter().Text(o.LicenceNumber ?? "").FontSize(10);
        });
    }

    private static void LicencesPage(IContainer c)
    {
        c.PaddingTop(14).Column(col =>
        {
            col.Item().Text("Licences, Ratings and Endorsements").FontSize(14);
            col.Item().PaddingTop(14).BorderBottom(0.75f).BorderColor("#111111").PaddingBottom(3).Row(r =>
            {
                r.RelativeItem(14).Text("Type");
                r.RelativeItem(23).Text("Description");
                r.RelativeItem(14).Text("Expiry Date");
                r.RelativeItem(29).Text("Instructor Endorsement");
                r.RelativeItem(20).Text("Instructor Signature");
            });
        });
    }

    // ---- the logbook table -----------------------------------------------------------------

    private static void Columns(TableDescriptor t) => t.ColumnsDefinition(cd => { foreach (var w in Widths) cd.RelativeColumn(w); });

    private static IContainer HeadCell(IContainer c) =>
        c.Border(0.5f).BorderColor(Line).Background(HeadBg).PaddingHorizontal(2).PaddingVertical(1.5f).AlignCenter();

    private static void TableHeader(IContainer c)
    {
        c.Table(t =>
        {
            Columns(t);
            // Row 1: column-group numbers.
            void Num(uint col, uint span, string text) =>
                t.Cell().Row(1).Column(col).ColumnSpan(span).Element(HeadCell).AlignMiddle().Text(text);
            Num(1, 1, "1"); Num(2, 2, "2"); Num(4, 2, "3"); Num(6, 2, "4"); Num(8, 3, "5"); Num(11, 1, "6");
            Num(12, 1, "7"); Num(13, 2, "8"); Num(15, 2, "9"); Num(17, 4, "10"); Num(21, 2, "11"); Num(23, 1, "12");

            // Rows 2–3: titles. Single cells span both rows.
            void Tall(uint col, string text, bool left = false)
            {
                var cell = t.Cell().Row(2).Column(col).RowSpan(2).Border(0.5f).BorderColor(Line).Background(HeadBg)
                    .PaddingHorizontal(2).PaddingVertical(2);
                (left ? cell.AlignLeft() : cell.AlignCenter()).Text(text);
            }
            void Group(uint col, uint span, string text) =>
                t.Cell().Row(2).Column(col).ColumnSpan(span).Element(HeadCell).Text(text);
            void Sub(uint col, string text) =>
                t.Cell().Row(3).Column(col).Element(HeadCell).MinHeight(20).Text(text);

            Tall(1, "Date");
            Group(2, 2, "Departure"); Sub(2, "Place"); Sub(3, "Time");
            Group(4, 2, "Arrival"); Sub(4, "Place"); Sub(5, "Time");
            Group(6, 2, "Aircraft"); Sub(6, "Model"); Sub(7, "Reg'n");
            Group(8, 2, "Single-Pilot"); Sub(8, "SE"); Sub(9, "ME");
            Tall(10, "Multi-\nPilot");
            Tall(11, "Total\nTime");
            Tall(12, "Name(s) PIC", left: true);
            Group(13, 2, "Landings"); Sub(13, "Day"); Sub(14, "Night");
            Group(15, 2, "Operational"); Sub(15, "Night"); Sub(16, "IFR");
            Group(17, 4, "Function"); Sub(17, "PIC"); Sub(18, "Co-\nPilot"); Sub(19, "Dual"); Sub(20, "Instr-\nuctor");
            Group(21, 2, "FSTD"); Sub(21, "Device"); Sub(22, "Time");
            Tall(23, "Remarks", left: true);
        });
    }

    private static void TableBody(IContainer c, LogbookEntry[] entries, int rows)
    {
        c.Table(t =>
        {
            Columns(t);
            for (var i = 0; i < rows; i++)
            {
                var e = i < entries.Length ? entries[i] : null;
                Cell(t, e?.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), left: true);
                Cell(t, e?.Departure, left: true);
                Cell(t, e?.OffBlock?.ToString("HH:mm", CultureInfo.InvariantCulture));
                Cell(t, e?.Arrival, left: true);
                Cell(t, e?.OnBlock?.ToString("HH:mm", CultureInfo.InvariantCulture));
                Cell(t, e?.TypeCode, left: true);
                Cell(t, e?.Registration, left: true);
                Cell(t, Hm(e?.SingleEngineMinutes));
                Cell(t, Hm(e?.MultiEngineMinutes));
                Cell(t, Hm(e?.MultiPilotMinutes));
                Cell(t, Hm(e?.TotalMinutes));
                Cell(t, e?.PicName, left: true);
                Cell(t, Count(e?.LandingsDay));
                Cell(t, Count(e?.LandingsNight));
                Cell(t, Hm(e?.NightMinutes));
                Cell(t, Hm(e?.IfrMinutes));
                Cell(t, Hm(e?.PicMinutes));
                Cell(t, Hm(e?.CoPilotMinutes));
                Cell(t, Hm(e?.DualMinutes));
                Cell(t, Hm(e?.InstructorMinutes));
                Cell(t, null);                                      // FSTD device
                Cell(t, null);                                      // FSTD time
                Cell(t, e?.Remarks, left: true);
            }
        });
    }

    private static void Cell(TableDescriptor t, string? text, bool left = false)
    {
        var c = t.Cell().Border(0.5f).BorderColor(Line).Height(RowHeight).PaddingHorizontal(2).AlignMiddle();
        if (string.IsNullOrEmpty(text)) return;
        (left ? c.AlignLeft() : c.AlignCenter()).Text(text).ClampLines(1);
    }

    private static void TotalsBlock(IContainer c, LogbookTotals thisPage, LogbookTotals previous, string pilotName)
    {
        var total = previous + thisPage;
        c.Table(t =>
        {
            Columns(t);

            // Certification, spanning the first five columns and all three rows.
            t.Cell().Row(1).Column(1).ColumnSpan(5).RowSpan(3).Border(0.5f).BorderColor(Line).Padding(4).Column(col =>
            {
                col.Item().AlignCenter().Text("I certify that the entries in this log are true");
                col.Item().PaddingTop(42).PaddingHorizontal(2).BorderTop(0.5f).BorderColor("#111111")
                    .PaddingTop(2).AlignCenter().Text(pilotName);
            });

            TotalsRow(t, 1, "Total\nThis Page", thisPage);
            TotalsRow(t, 2, "Total From\nPrevious Pages", previous);
            TotalsRow(t, 3, "Total\nTime", total);
        });
    }

    private static void TotalsRow(TableDescriptor t, uint row, string label, LogbookTotals v)
    {
        t.Cell().Row(row).Column(6).ColumnSpan(2).Border(0.5f).BorderColor(Line).Height(26).PaddingHorizontal(3)
            .AlignMiddle().AlignRight().Text(label).AlignRight();

        void Two(uint col, long? minutes) => Box(t, row, col, minutes is > 0 ? $"{minutes / 60}\n{minutes % 60:00}" : null);
        void One(uint col, int n) => Box(t, row, col, n > 0 ? n.ToString(CultureInfo.InvariantCulture) : null);

        Two(8, v.SingleEngine); Two(9, v.MultiEngine);
        Two(10, v.MultiPilot);
        Two(11, v.Total);
        Box(t, row, 12, null);                          // Name PIC
        One(13, v.LandingsDay); One(14, v.LandingsNight);
        Two(15, v.Night); Two(16, v.Ifr);
        Two(17, v.Pic); Two(18, v.CoPilot); Two(19, v.Dual); Two(20, v.Instructor);
        Box(t, row, 21, null);                          // FSTD device
        Two(22, v.Simulator);                           // FSTD time
        t.Cell().Row(row).Column(23).PaddingLeft(6).AlignMiddle().Text("hours\nminutes");
    }

    private static void Box(TableDescriptor t, uint row, uint col, string? text)
    {
        var c = t.Cell().Row(row).Column(col).Border(0.5f).BorderColor(Line).Height(26).AlignMiddle().AlignCenter();
        if (text is not null) c.Text(text).AlignCenter();
    }

    private static string? Hm(int? minutes) => minutes is > 0 ? $"{minutes / 60}:{minutes % 60:00}" : null;
    private static string? Count(int? n) => n is > 0 ? n.Value.ToString(CultureInfo.InvariantCulture) : null;
}
