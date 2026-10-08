namespace FlightCrewStudio.Components.Shared;

/// <summary>OurAirports continent codes.</summary>
public static class Continents
{
    public static readonly (string Code, string Name)[] All =
    [
        ("AF", "Africa"), ("AN", "Antarctica"), ("AS", "Asia"), ("EU", "Europe"),
        ("NA", "North America"), ("OC", "Oceania"), ("SA", "South America")
    ];

    public static string? Name(string? code) =>
        code is null ? null : All.FirstOrDefault(c => c.Code == code).Name ?? code;
}
