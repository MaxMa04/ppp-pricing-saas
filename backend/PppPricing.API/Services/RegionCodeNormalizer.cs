using System.Globalization;

namespace PppPricing.API.Services;

public static class RegionCodeNormalizer
{
    private static readonly Dictionary<string, string> Alpha2Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UK", "GBR" },
        { "EL", "GRC" },
        { "XK", "XKX" },
    };

    private static readonly Dictionary<string, string> Alpha3Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        { "XKX", "XK" },
        { "EUZ", "EU" },
    };

    private static readonly Dictionary<string, string> Alpha2ToAlpha3 = BuildAlpha2ToAlpha3();
    private static readonly Dictionary<string, string> Alpha3ToAlpha2 = BuildAlpha3ToAlpha2();

    public static string? NormalizeToAlpha3(string? regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return null;
        }

        var code = regionCode.Trim().ToUpperInvariant();
        if (code.Length == 3)
        {
            return code;
        }

        if (code.Length != 2)
        {
            return null;
        }

        if (Alpha2Overrides.TryGetValue(code, out var overrideCode))
        {
            return overrideCode;
        }

        return Alpha2ToAlpha3.GetValueOrDefault(code);
    }

    public static string? NormalizeToAlpha2(string? regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return null;
        }

        var code = regionCode.Trim().ToUpperInvariant();
        if (code.Length == 2)
        {
            return code;
        }

        if (code.Length != 3)
        {
            return null;
        }

        if (Alpha3Overrides.TryGetValue(code, out var overrideCode))
        {
            return overrideCode;
        }

        return Alpha3ToAlpha2.GetValueOrDefault(code);
    }

    private static Dictionary<string, string> BuildAlpha2ToAlpha3()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

        foreach (var culture in cultures)
        {
            RegionInfo? region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch
            {
                continue;
            }

            var alpha2 = region.TwoLetterISORegionName?.ToUpperInvariant();
            var alpha3 = region.ThreeLetterISORegionName?.ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(alpha2) || string.IsNullOrWhiteSpace(alpha3))
            {
                continue;
            }

            if (alpha2.Length == 2 && alpha3.Length == 3)
            {
                map[alpha2] = alpha3;
            }
        }

        return map;
    }

    private static Dictionary<string, string> BuildAlpha3ToAlpha2()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (alpha2, alpha3) in Alpha2ToAlpha3)
        {
            if (!map.ContainsKey(alpha3))
            {
                map[alpha3] = alpha2;
            }
        }

        foreach (var (alpha3, alpha2) in Alpha3Overrides)
        {
            map[alpha3] = alpha2;
        }

        return map;
    }
}
