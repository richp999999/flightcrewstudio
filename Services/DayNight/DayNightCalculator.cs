namespace FlightCrewStudio.Services.DayNight;

/// <summary>Which sun angle separates day from night.</summary>
public enum NightDefinition
{
    /// <summary>Night is between sunset and sunrise (sun's upper edge on the horizon, i.e. centre at −0.833°).</summary>
    SunsetToSunrise = 0,
    /// <summary>Night is between the end of evening and start of morning civil twilight (sun centre at −6°), as in EASA/UK rules.</summary>
    CivilTwilight = 1
}

/// <summary>Day and night time for one flight, in whole minutes (Day + Night = block time).</summary>
public readonly record struct DayNightResult(int DayMinutes, int NightMinutes)
{
    public int TotalMinutes => DayMinutes + NightMinutes;
}

/// <summary>
/// Works out how much of a flight was flown in daylight and how much at night.
///
/// The aircraft is assumed to move along the great-circle route from the departure airport
/// (at off-block time) to the destination (at on-block time) at a constant rate. The route is
/// split into 10-minute intervals plus a final shorter interval for any remainder. At each
/// interval boundary the aircraft's position is found on the great circle and the sun's
/// elevation is computed for that place and moment: below the horizon (sunset→sunrise) is night,
/// otherwise day. When an interval starts and ends in the same state, the whole interval counts
/// as that state; when it changes (sunrise or sunset is crossed inside the interval) the exact
/// moment of the change is found by bisection, so the result is accurate to the minute rather
/// than to the nearest 10 minutes.
///
/// The sun's position uses the NOAA solar-position equations (accurate to well under a minute of
/// sunrise/sunset for dates in this century). The horizon is taken at sea level.
/// </summary>
public static class DayNightCalculator
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <summary>Calculate day/night minutes for a flight. Returns null if the times are unusable.</summary>
    public static DayNightResult? Calculate(DateTime offBlockUtc, DateTime onBlockUtc,
        double depLat, double depLon, double arrLat, double arrLon,
        NightDefinition definition = NightDefinition.SunsetToSunrise)
    {
        if (onBlockUtc <= offBlockUtc) return null;
        var total = onBlockUtc - offBlockUtc;
        if (total > TimeSpan.FromHours(24)) return null;   // implausible block time

        var threshold = definition == NightDefinition.CivilTwilight ? -6.0 : -0.833;
        var route = new GreatCircle(depLat, depLon, arrLat, arrLon);

        bool IsNight(double fraction)
        {
            var t = offBlockUtc + TimeSpan.FromTicks((long)(total.Ticks * fraction));
            var (lat, lon) = route.PointAt(fraction);
            return SolarPosition.ElevationDegrees(t, lat, lon) < threshold;
        }

        double nightSeconds = 0;
        var totalSeconds = total.TotalSeconds;
        var start = 0.0;                      // seconds from off-block
        var startNight = IsNight(0);

        while (start < totalSeconds)
        {
            var end = Math.Min(start + Interval.TotalSeconds, totalSeconds);   // 10 min, or the remainder
            var endNight = IsNight(end / totalSeconds);

            if (startNight == endNight)
            {
                if (startNight) nightSeconds += end - start;
            }
            else
            {
                // Sunrise or sunset happens inside this interval: find it to within ~1 second.
                double lo = start, hi = end;
                while (hi - lo > 1)
                {
                    var mid = (lo + hi) / 2;
                    if (IsNight(mid / totalSeconds) == startNight) lo = mid; else hi = mid;
                }
                var change = (lo + hi) / 2;
                nightSeconds += startNight ? change - start : end - change;
            }

            start = end;
            startNight = endNight;
        }

        var totalMinutes = (int)Math.Round(totalSeconds / 60.0);
        var night = Math.Clamp((int)Math.Round(nightSeconds / 60.0), 0, totalMinutes);
        return new DayNightResult(totalMinutes - night, night);
    }

    /// <summary>Is it night at this place and time?</summary>
    public static bool IsNight(DateTime utc, double lat, double lon, NightDefinition definition = NightDefinition.SunsetToSunrise) =>
        SolarPosition.ElevationDegrees(utc, lat, lon) < (definition == NightDefinition.CivilTwilight ? -6.0 : -0.833);
}

/// <summary>Points along the great circle between two positions (spherical earth).</summary>
public sealed class GreatCircle
{
    private readonly double _lat1, _lon1, _lat2, _lon2, _d;

    public GreatCircle(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
    {
        _lat1 = Rad(lat1Deg); _lon1 = Rad(lon1Deg); _lat2 = Rad(lat2Deg); _lon2 = Rad(lon2Deg);
        // Central angle (haversine form, stable for short distances).
        var dLat = _lat2 - _lat1; var dLon = _lon2 - _lon1;
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(_lat1) * Math.Cos(_lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        _d = 2 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Great-circle distance in nautical miles.</summary>
    public double DistanceNm => _d * 3440.065;

    /// <summary>Position (degrees) at fraction f (0 = departure, 1 = destination) of the way along the route.</summary>
    public (double Lat, double Lon) PointAt(double f)
    {
        if (_d < 1e-9) return (Deg(_lat1), Deg(_lon1));
        var a = Math.Sin((1 - f) * _d) / Math.Sin(_d);
        var b = Math.Sin(f * _d) / Math.Sin(_d);
        var x = a * Math.Cos(_lat1) * Math.Cos(_lon1) + b * Math.Cos(_lat2) * Math.Cos(_lon2);
        var y = a * Math.Cos(_lat1) * Math.Sin(_lon1) + b * Math.Cos(_lat2) * Math.Sin(_lon2);
        var z = a * Math.Sin(_lat1) + b * Math.Sin(_lat2);
        return (Deg(Math.Atan2(z, Math.Sqrt(x * x + y * y))), Deg(Math.Atan2(y, x)));
    }

    private static double Rad(double d) => d * Math.PI / 180;
    private static double Deg(double r) => r * 180 / Math.PI;
}

/// <summary>Sun elevation using the NOAA solar position algorithm.</summary>
public static class SolarPosition
{
    public static double ElevationDegrees(DateTime utc, double latDeg, double lonDeg)
    {
        utc = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
        var jd = utc.ToOADate() + 2415018.5;                 // Julian day
        var t = (jd - 2451545.0) / 36525.0;                   // Julian centuries since J2000

        var l0 = Mod(280.46646 + t * (36000.76983 + t * 0.0003032), 360);
        var m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        var e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        var mr = Rad(m);
        var c = Math.Sin(mr) * (1.914602 - t * (0.004817 + 0.000014 * t))
              + Math.Sin(2 * mr) * (0.019993 - 0.000101 * t)
              + Math.Sin(3 * mr) * 0.000289;
        var trueLong = l0 + c;
        var omega = 125.04 - 1934.136 * t;
        var lambda = trueLong - 0.00569 - 0.00478 * Math.Sin(Rad(omega));
        var eps0 = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
        var eps = eps0 + 0.00256 * Math.Cos(Rad(omega));
        var decl = Math.Asin(Math.Sin(Rad(eps)) * Math.Sin(Rad(lambda)));

        var y = Math.Pow(Math.Tan(Rad(eps) / 2), 2);
        var l0r = Rad(l0);
        var eqTime = 4 * Deg(y * Math.Sin(2 * l0r) - 2 * e * Math.Sin(mr) + 4 * e * y * Math.Sin(mr) * Math.Cos(2 * l0r)
                             - 0.5 * y * y * Math.Sin(4 * l0r) - 1.25 * e * e * Math.Sin(2 * mr));   // minutes

        var minutes = utc.TimeOfDay.TotalMinutes;
        var trueSolarTime = Mod(minutes + eqTime + 4 * lonDeg, 1440);
        var hourAngle = Rad(trueSolarTime / 4 - 180);
        var lat = Rad(latDeg);
        var cosZenith = Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(hourAngle);
        return 90 - Deg(Math.Acos(Math.Clamp(cosZenith, -1, 1)));
    }

    private static double Mod(double a, double n) => ((a % n) + n) % n;
    private static double Rad(double d) => d * Math.PI / 180;
    private static double Deg(double r) => r * 180 / Math.PI;
}
