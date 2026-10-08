using System.Text;

namespace FlightCrewStudio.Services;

/// <summary>Minimal RFC 4180 CSV reader (quoted fields, doubled quotes, CR/LF line endings).</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { /* ignore; \n ends the row */ }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    /// <summary>
    /// A CSV with a header row: <see cref="Rows"/> are the data rows and <see cref="Get"/> reads a
    /// value by column name (blank → null).
    /// </summary>
    public sealed class Table
    {
        private readonly Dictionary<string, int> _cols;
        public List<List<string>> Rows { get; }

        public Table(string text)
        {
            if (text.Length > 0 && text[0] == '﻿') text = text[1..]; // BOM
            var all = Parse(text);
            if (all.Count == 0) throw new FormatException("The file is empty.");
            _cols = all[0].Select((h, i) => (h: h.Trim(), i)).GroupBy(x => x.h, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
            Rows = all.Skip(1).Where(r => r.Count > 1 || (r.Count == 1 && r[0].Length > 0)).ToList();
        }

        public bool Has(string column) => _cols.ContainsKey(column);

        public void Require(string what, params string[] columns)
        {
            var missing = columns.Where(c => !Has(c)).ToList();
            if (missing.Count > 0)
                throw new FormatException($"This doesn't look like an OurAirports {what} file — missing column(s): {string.Join(", ", missing)}.");
        }

        public string? Get(List<string> row, string column) =>
            _cols.TryGetValue(column, out var i) && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
    }
}
