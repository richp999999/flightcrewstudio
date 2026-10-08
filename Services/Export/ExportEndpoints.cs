using System.Globalization;
using System.Security.Claims;
using FlightCrewStudio.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Export;

/// <summary>Form posted by the Export page.</summary>
public sealed class ExportForm
{
    public string? From { get; set; }
    public string? To { get; set; }
    public bool IncludePassive { get; set; }
    public string? PilotName { get; set; }
    public string? LicenceNumber { get; set; }
    public int RowsPerPage { get; set; } = 14;
    public bool CoverPages { get; set; }
}

/// <summary>
/// Download endpoints for the Export page: POST /export/pdf (EASA FCL.050 logbook) and
/// POST /export/csv. They are plain form posts (with antiforgery) so the browser handles the
/// file download directly.
/// </summary>
public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/export").RequireAuthorization();

        group.MapPost("/pdf", async ([FromForm] ExportForm form, ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> dbFactory, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

            // Remember the name and licence number for next time.
            var name = Clean(form.PilotName, 100) ?? user.LogbookName ?? user.DisplayName ?? user.UserName ?? "";
            var licence = Clean(form.LicenceNumber, 50);
            if (user.LogbookName != name || user.LicenceNumber != licence)
            {
                user.LogbookName = name;
                user.LicenceNumber = licence;
                await db.SaveChangesAsync(ct);
            }

            var filter = Filter(form);
            var (entries, broughtForward) = await LogbookEntries.LoadAsync(db, userId, name, filter, ct);
            var pdf = EasaPdfExporter.Generate(entries, broughtForward,
                new EasaPdfOptions(name, licence, form.RowsPerPage, form.CoverPages, DateTime.UtcNow));
            return Results.File(pdf, "application/pdf", $"logbook-easa{FileSuffix(filter)}.pdf");
        });

        group.MapPost("/csv", async ([FromForm] ExportForm form, ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> dbFactory, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
            var filter = Filter(form);
            var (entries, _) = await LogbookEntries.LoadAsync(db, userId, user.LogbookName ?? user.DisplayName, filter, ct);
            return Results.File(LogbookCsvExporter.Write(entries), "text/csv", $"logbook{FileSuffix(filter)}.csv");
        });

        return app;
    }

    private static ExportFilter Filter(ExportForm f) => new(ParseDate(f.From), ParseDate(f.To), f.IncludePassive);

    private static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string FileSuffix(ExportFilter f) =>
        f.From is null && f.To is null ? $"-{DateTime.UtcNow:yyyy-MM-dd}"
            : $"-{f.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "start"}-to-{f.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "now"}";

    private static string? Clean(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();
}
