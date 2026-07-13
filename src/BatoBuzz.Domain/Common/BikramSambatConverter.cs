namespace BatoBuzz.Domain.Common;

/// <summary>
/// Converts between Gregorian (AD) and Bikram Sambat (BS) dates.
/// BS months do not have a fixed number of days, so conversion is table-driven
/// rather than formula-based. The table below gives the day count for each of
/// the 12 BS months (plus the year total) for every supported BS year, based on
/// publicly published Nepali calendar data. It covers 1975-2099 BS, anchored at
/// 1975-01-01 BS = 1918-04-13 AD, matching the range used by widely-deployed
/// open-source Nepali calendar converters (MIT-licensed data/algorithm shape).
/// </summary>
public static class BikramSambatConverter
{
    private const int MinBsYear = 1975;
    private const int MaxBsYear = 2099;
    private static readonly DateTime EpochStartAd = new(1918, 4, 13);

    public static readonly string[] MonthNames =
    {
        "Baishakh", "Jestha", "Ashadh", "Shrawan", "Bhadra", "Ashwin",
        "Kartik", "Mangsir", "Poush", "Magh", "Falgun", "Chaitra"
    };

    private static readonly Dictionary<int, int[]> MonthDays = new()
    {
        { 1975, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 1976, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 1977, new[] { 30, 32, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31, 365 } },
        { 1978, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1979, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 1980, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 1981, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31, 365 } },
        { 1982, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1983, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 1984, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 1985, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 1986, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1987, new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 1988, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 1989, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1990, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1991, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 1992, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 1993, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1994, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1995, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 1996, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 1997, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 1998, new[] { 31, 31, 32, 31, 32, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 1999, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2000, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2001, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2002, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2003, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2004, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2005, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2006, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2007, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2008, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31, 365 } },
        { 2009, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2010, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2011, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2012, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 2013, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2014, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2015, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2016, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 2017, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2018, new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2019, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 2020, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2021, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2022, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 2023, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 2024, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2025, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2026, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2027, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2028, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2029, new[] { 31, 31, 32, 31, 32, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2030, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2031, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2032, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2033, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2034, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2035, new[] { 30, 32, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31, 365 } },
        { 2036, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2037, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2038, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2039, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 2040, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2041, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2042, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2043, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 2044, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2045, new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2046, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2047, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2048, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2049, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 2050, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 2051, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2052, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2053, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 2054, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 2055, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2056, new[] { 31, 31, 32, 31, 32, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2057, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2058, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2059, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2060, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2061, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2062, new[] { 30, 32, 31, 32, 31, 31, 29, 30, 29, 30, 29, 31, 365 } },
        { 2063, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2064, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2065, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2066, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31, 365 } },
        { 2067, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2068, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2069, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2070, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 2071, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2072, new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2073, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2074, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2075, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2076, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 2077, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 2078, new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2079, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2080, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30, 365 } },
        { 2081, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 366 } },
        { 2082, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2083, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2084, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2085, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2086, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2087, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2088, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2089, new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31, 365 } },
        { 2090, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2091, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2092, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2093, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31, 365 } },
        { 2094, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2095, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
        { 2096, new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31, 366 } },
        { 2097, new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30, 365 } },
        { 2098, new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30, 365 } },
        { 2099, new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30, 365 } },
    };

    /// <summary>Minimum AD date supported by the conversion table (1975-01-01 BS).</summary>
    public static readonly DateTime MinSupportedAdDate = EpochStartAd;

    /// <summary>Maximum AD date supported by the conversion table (2099-12-30 BS, inclusive).</summary>
    public static readonly DateTime MaxSupportedAdDate = ToGregorian(MaxBsYear, 12, MonthDays[MaxBsYear][11]);

    /// <summary>Converts a Gregorian date to its Bikram Sambat equivalent.</summary>
    public static (int Year, int Month, int Day) ToBikramSambat(DateTime adDate)
    {
        var target = adDate.Date;
        var remaining = (target - EpochStartAd).Days;
        if (remaining < 0)
            throw new ArgumentOutOfRangeException(nameof(adDate), adDate, $"Date is before the supported Bikram Sambat range ({EpochStartAd:yyyy-MM-dd} AD).");

        for (var year = MinBsYear; year <= MaxBsYear; year++)
        {
            var days = MonthDays[year];
            var totalDaysInYear = days[12];
            if (remaining >= totalDaysInYear)
            {
                remaining -= totalDaysInYear;
                continue;
            }

            for (var month = 0; month < 12; month++)
            {
                if (remaining >= days[month])
                {
                    remaining -= days[month];
                    continue;
                }

                return (year, month + 1, remaining + 1);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(adDate), adDate, $"Date is after the supported Bikram Sambat range (BS {MaxBsYear}).");
    }

    /// <summary>Converts a Bikram Sambat date to its Gregorian equivalent.</summary>
    public static DateTime ToGregorian(int bsYear, int bsMonth, int bsDay)
    {
        if (bsYear < MinBsYear || bsYear > MaxBsYear)
            throw new ArgumentOutOfRangeException(nameof(bsYear), bsYear, $"Bikram Sambat year must be between {MinBsYear} and {MaxBsYear}.");
        if (bsMonth < 1 || bsMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(bsMonth), bsMonth, "Bikram Sambat month must be between 1 and 12.");

        var days = MonthDays[bsYear];
        if (bsDay < 1 || bsDay > days[bsMonth - 1])
            throw new ArgumentOutOfRangeException(nameof(bsDay), bsDay, $"Bikram Sambat day must be between 1 and {days[bsMonth - 1]} for month {bsMonth} of year {bsYear}.");

        var totalDays = 0;
        for (var year = MinBsYear; year < bsYear; year++)
            totalDays += MonthDays[year][12];
        for (var month = 0; month < bsMonth - 1; month++)
            totalDays += days[month];
        totalDays += bsDay - 1;

        return EpochStartAd.AddDays(totalDays);
    }

    /// <summary>Formats an AD date as a "YYYY-MM-DD" Bikram Sambat string.</summary>
    public static string ToBsDateString(DateTime adDate)
    {
        var (year, month, day) = ToBikramSambat(adDate);
        return $"{year:D4}-{month:D2}-{day:D2}";
    }

    /// <summary>Formats an AD date as a human-readable Bikram Sambat string, e.g. "15 Shrawan 2082".</summary>
    public static string ToBsDisplayString(DateTime adDate)
    {
        var (year, month, day) = ToBikramSambat(adDate);
        return $"{day} {MonthNames[month - 1]} {year}";
    }

    /// <summary>True if the given AD date falls within the supported BS conversion range.</summary>
    public static bool IsSupported(DateTime adDate) =>
        adDate.Date >= MinSupportedAdDate && adDate.Date <= MaxSupportedAdDate;

    /// <summary>
    /// Exact Shrawan 1 start of the Nepali fiscal year containing
    /// <paramref name="asOfDate"/>, derived from the table-backed BS calendar.
    /// </summary>
    public static DateTime GetCurrentNepaliFiscalYearStart(DateTime asOfDate)
    {
        if (!IsSupported(asOfDate))
            throw new ArgumentOutOfRangeException(
                nameof(asOfDate), asOfDate, "The date is outside the supported Bikram Sambat calendar range.");

        var (bsYear, bsMonth, _) = ToBikramSambat(asOfDate);
        var fiscalStartBsYear = bsMonth >= 4 ? bsYear : bsYear - 1;
        return ToGregorian(fiscalStartBsYear, 4, 1);
    }

    /// <summary>
    /// Exact Shrawan-to-Ashadh fiscal period containing <paramref name="asOfDate"/>.
    /// The end is the day before the next table-backed Shrawan 1.
    /// </summary>
    public static (DateTime StartDate, DateTime EndDate) GetNepaliFiscalYearPeriod(DateTime asOfDate)
    {
        var startDate = GetCurrentNepaliFiscalYearStart(asOfDate);
        var (startBsYear, _, _) = ToBikramSambat(startDate);
        if (startBsYear >= MaxBsYear)
            throw new ArgumentOutOfRangeException(
                nameof(asOfDate), asOfDate, "The fiscal-year end is outside the supported Bikram Sambat calendar range.");

        var nextStartDate = ToGregorian(startBsYear + 1, 4, 1);
        return (startDate, nextStartDate.AddDays(-1));
    }
}
