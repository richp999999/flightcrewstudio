using FlightCrewStudio.Data;

namespace FlightCrewStudio.Components.Shared;

/// <summary>Small display helpers shared by the logbook-style pages.</summary>
public static class Fmt
{
    /// <summary>Minutes → "h:mm" (e.g. 219 → "3:39", 706,724 → "11778:44").</summary>
    public static string Hm(long minutes) => minutes <= 0 ? "" : $"{minutes / 60}:{minutes % 60:00}";

    /// <summary>Same as <see cref="Hm"/> but shows "0:00" instead of blank.</summary>
    public static string HmZero(long minutes) => $"{minutes / 60}:{minutes % 60:00}";

    /// <summary>
    /// Parse a duration typed as "HHHH:MM" (e.g. "3557:54") or whole hours ("3557"). Blank → 0.
    /// Returns null if it isn't a valid duration (minutes must be 00–59).
    /// </summary>
    public static int? ParseHm(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(text.Trim(), @"^(\d{1,5})(?::([0-5]\d))?$");
        if (!m.Success) return null;
        return int.Parse(m.Groups[1].Value) * 60 + (m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0);
    }

    public static int BlockMinutes(Flight f) =>
        f.OffBlock is { } off && f.OnBlock is { } on && on > off ? (int)Math.Round((on - off).TotalMinutes) : 0;

    private static readonly string[] OneLetter = ["G", "D", "F", "I", "C", "M", "N"];
    private static readonly string[] TwoLetter = ["EI", "OE", "OH", "OY", "SE", "LN", "OO", "PH", "HB", "EC", "CS", "SP", "9H", "5B", "LX", "OK", "OM", "HA", "YR", "LZ", "SX", "TC", "9A", "S5", "YU", "ES", "YL", "LY", "UR", "TF", "VP", "VQ", "ZS", "VH", "ZK"];

    /// <summary>"GHLYH" → "G-HLYH", "EIFMV" → "EI-FMV"; leaves anything already hyphenated or unknown alone.</summary>
    public static string Registration(string? reg)
    {
        if (string.IsNullOrWhiteSpace(reg)) return "";
        reg = reg.Trim().ToUpperInvariant();
        if (reg.Contains('-') || reg.Length < 4) return reg;
        foreach (var p in TwoLetter)
            if (reg.StartsWith(p) && reg.Length == p.Length + 3) return $"{p}-{reg[p.Length..]}";
        foreach (var p in OneLetter)
            if (reg.StartsWith(p) && reg.Length == p.Length + 4 && p != "N") return $"{p}-{reg[p.Length..]}";
        return reg;
    }

    /// <summary>"Richard PALMER" → "RP"; "40016792" → "#".</summary>
    public static string Initials(CrewMember? m)
    {
        if (m?.Name is not { Length: > 0 } name) return "#";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>Stable pleasant colour per person, so the same face keeps the same avatar colour.</summary>
    public static string AvatarColour(int id)
    {
        string[] palette = ["#3e6f72", "#3b71ca", "#6d5ba7", "#b0603a", "#2f7d4f", "#8a6d1f", "#a23f5a", "#4f6378"];
        return palette[Math.Abs(id) % palette.Length];
    }
}
