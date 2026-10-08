using System.Globalization;
using System.Text;

namespace FlightCrewStudio.Services.Export;

/// <summary>
/// Writes the logbook as CSV in logbook.aero's export layout (same columns, same formats: dates
/// yyyy-MM-dd, times HH:mm UTC with Timezone "U", durations h:mm), so the file can be imported
/// into logbook.aero or opened in a spreadsheet. There are at least three PersonN / RoleN column
/// pairs, and more when a flight has more flight-deck crew (as logbook.aero does).
/// </summary>
public static class LogbookCsvExporter
{
    public static readonly string[] Columns =
    [
        "Date", "Flight_Number", "Departure_Airfield", "Departure_Time", "Arrival_Airfield", "Arrival_Time", "Timezone",
        "Aircraft_Type", "Aircraft_Registration", "Aircraft_Tags", "Total_Time", "MultiPilot_Time", "PIC_Name",
        "Takeoff_Day", "Landing_Day", "Takeoff_Night", "Landing_Night", "Night_Time", "IFR_Time", "XC_Time", "PIC_Time",
        "CoPilot_Time", "Dual_Time", "PICUS_Time", "Instructor_Time",
        "Custom1_Time", "Custom2_Time", "Custom3_Time", "Custom4_Time", "Custom5_Time", "Custom6_Time", "Custom7_Time",
        "Custom8_Time", "Custom9_Time", "Custom10_Time", "Solo_Time", "Complex_Time", "SimulatedInstrument_Time",
        "Simulator_Time", "Sim_Type", "Remarks", "Pilot_Flying", "Pilot_Monitoring", "Relief_Crew",
        "Custom1_Number", "Custom2_Number", "Custom3_Number", "Custom4_Number", "Category", "Flight_Tags",
        "TachoStart", "TachoEnd", "HobbsStart", "HobbsEnd", "Duty_Start", "Duty_End", "Duty_Time",
        "CostRental", "CostInstructor", "CostLanding", "CostParking", "CostHandling", "CostFuel", "CostOther",
        PersonColumnsMarker,
        "Approach1_Landing", "Approach1_Approach", "Approach1_Quantity", "Approach1_Airfield", "Approach1_Runway", "Approach1_Text",
    ];

    private const string PersonColumnsMarker = "<persons>";

    public static byte[] Write(IEnumerable<LogbookEntry> entries)
    {
        var list = entries.ToList();
        var people = Math.Max(3, list.Count == 0 ? 0 : list.Max(e => e.FlightDeck.Count));
        var columns = Columns.SelectMany(c => c == PersonColumnsMarker
            ? Enumerable.Range(1, people).SelectMany(i => new[] { $"Person{i}", $"Role{i}" })
            : [c]).ToList();

        var sb = new StringBuilder();
        sb.Append(string.Join(",", columns)).Append('\n');
        foreach (var e in list)
        {
            var v = new Dictionary<string, string?>
            {
                ["Date"] = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["Flight_Number"] = e.FlightNumber,
                ["Departure_Airfield"] = e.Departure,
                ["Departure_Time"] = e.OffBlock?.ToString("HH:mm", CultureInfo.InvariantCulture),
                ["Arrival_Airfield"] = e.Arrival,
                ["Arrival_Time"] = e.OnBlock?.ToString("HH:mm", CultureInfo.InvariantCulture),
                ["Timezone"] = "U",
                ["Aircraft_Type"] = e.TypeCode is null ? null : $"{e.TypeCode},{e.TypeManufacturer},{e.TypeModel}",
                ["Aircraft_Registration"] = e.Registration,
                ["Total_Time"] = Hm(e.TotalMinutes),
                ["MultiPilot_Time"] = Hm(e.MultiPilotMinutes),
                ["PIC_Name"] = e.PicName,
                ["Takeoff_Day"] = e.TakeoffsDay.ToString(CultureInfo.InvariantCulture),
                ["Landing_Day"] = e.LandingsDay.ToString(CultureInfo.InvariantCulture),
                ["Takeoff_Night"] = e.TakeoffsNight.ToString(CultureInfo.InvariantCulture),
                ["Landing_Night"] = e.LandingsNight.ToString(CultureInfo.InvariantCulture),
                ["Night_Time"] = Hm(e.NightMinutes),
                ["IFR_Time"] = Hm(e.IfrMinutes),
                ["PIC_Time"] = Hm(e.PicMinutes),
                ["CoPilot_Time"] = Hm(e.CoPilotMinutes),
                ["Dual_Time"] = Hm(e.DualMinutes),
                ["Instructor_Time"] = Hm(e.InstructorMinutes),
                ["Remarks"] = e.Remarks,
                ["Pilot_Flying"] = e.PilotFlying == true ? "1" : "0",
                ["Pilot_Monitoring"] = e.PilotFlying == false ? "1" : "0",
                ["Relief_Crew"] = "0",
                ["Approach1_Landing"] = e.LandingsDay + e.LandingsNight > 0 || e.PilotFlying is not null ? (e.NightLanding ? "night" : "day") : null,
                ["Approach1_Quantity"] = (e.LandingsDay + e.LandingsNight).ToString(CultureInfo.InvariantCulture),
                ["Approach1_Airfield"] = e.Arrival,
            };
            for (var i = 0; i < e.FlightDeck.Count; i++)
            {
                v[$"Person{i + 1}"] = e.FlightDeck[i].Name;
                v[$"Role{i + 1}"] = e.FlightDeck[i].Role;
            }
            sb.Append(string.Join(",", columns.Select(c => Quote(v.GetValueOrDefault(c))))).Append('\n');
        }
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }

    private static string? Hm(int minutes) => minutes <= 0 ? null : $"{minutes / 60}:{minutes % 60:00}";

    private static string Quote(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.IndexOfAny([',', '"', '\n', '\r', ' ']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
