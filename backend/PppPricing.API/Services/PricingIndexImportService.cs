using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;

namespace PppPricing.API.Services;

public interface IPricingIndexImportService
{
    Task<ImportResult> ImportBigMacIndexAsync();
    Task<ImportResult> ImportNetflixIndexAsync(string? planType = null);
    Task<ImportResult> ImportWageDataAsync();
    Task<ImportResult> CalculateBigMacWorkingHoursAsync();
}

public class PricingIndexImportService : IPricingIndexImportService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PricingIndexImportService> _logger;

    private const string BigMacCsvUrl = "https://raw.githubusercontent.com/TheEconomist/big-mac-data/master/output-data/big-mac-full-index.csv";
    private const string NetflixJsonUrl = "https://raw.githubusercontent.com/tompec/netflix-prices/main/data/latest.json";
    private static readonly string[] SupportedNetflixPlans = ["mobile", "basic", "standard", "premium"];

    // Average hourly wages in USD (approximation based on GDP per capita and average work hours)
    private static readonly Dictionary<string, decimal> HourlyWagesUsd = new(StringComparer.OrdinalIgnoreCase)
    {
        { "US", 34.0m }, { "CH", 45.0m }, { "NO", 40.0m }, { "AU", 32.0m },
        { "DK", 38.0m }, { "DE", 30.0m }, { "GB", 28.0m }, { "CA", 29.0m },
        { "NL", 30.0m }, { "SE", 32.0m }, { "JP", 22.0m }, { "FR", 27.0m },
        { "KR", 18.0m }, { "IT", 24.0m }, { "ES", 20.0m }, { "SG", 24.0m },
        { "HK", 20.0m }, { "NZ", 26.0m }, { "IL", 22.0m }, { "TW", 12.0m },
        { "CZ", 14.0m }, { "PL", 12.0m }, { "HU", 10.0m }, { "PT", 14.0m },
        { "GR", 12.0m }, { "RU", 6.0m }, { "TR", 5.0m }, { "MX", 4.5m },
        { "BR", 5.0m }, { "CL", 7.0m }, { "AR", 4.0m }, { "CO", 3.5m },
        { "ZA", 5.0m }, { "TH", 3.0m }, { "MY", 5.0m }, { "CN", 6.0m },
        { "IN", 2.0m }, { "ID", 2.5m }, { "PH", 2.0m }, { "VN", 1.5m },
        { "EG", 1.5m }, { "PK", 1.0m }, { "UA", 3.0m }, { "RO", 8.0m },
        { "SA", 12.0m }, { "AE", 15.0m }, { "QA", 18.0m }, { "KW", 16.0m },
    };

    public PricingIndexImportService(
        ApplicationDbContext context,
        HttpClient httpClient,
        ILogger<PricingIndexImportService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ImportResult> ImportBigMacIndexAsync()
    {
        _logger.LogInformation("=== Starting Big Mac Index import ===");
        _logger.LogInformation("URL: {Url}", BigMacCsvUrl);

        try
        {
            var response = await _httpClient.GetAsync(BigMacCsvUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new ImportResult { Success = false, ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }

            var csvContent = await response.Content.ReadAsStringAsync();
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 2)
            {
                return new ImportResult { Success = false, ErrorMessage = "CSV file is empty or invalid" };
            }

            var header = ParseCsvLine(lines[0]);
            var dateIndex = Array.IndexOf(header, "date");
            var isoA3Index = Array.IndexOf(header, "iso_a3");
            var nameIndex = Array.IndexOf(header, "name");
            var currencyIndex = Array.IndexOf(header, "currency_code");
            var localPriceIndex = Array.IndexOf(header, "local_price");
            var dollarPriceIndex = Array.IndexOf(header, "dollar_price");
            var dollarExIndex = Array.IndexOf(header, "dollar_ex");
            var usdRawIndex = Array.IndexOf(header, "USD_raw");

            if (dateIndex < 0 || isoA3Index < 0 || dollarPriceIndex < 0)
            {
                return new ImportResult { Success = false, ErrorMessage = "Required columns not found in CSV" };
            }

            var latestByCountry = new Dictionary<string, (DateTime Date, string[] Fields)>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                var fields = ParseCsvLine(lines[i]);
                if (fields.Length <= Math.Max(dateIndex, Math.Max(isoA3Index, dollarPriceIndex)))
                {
                    continue;
                }

                if (!DateTime.TryParse(fields[dateIndex], CultureInfo.InvariantCulture, out var rowDate))
                {
                    continue;
                }

                var iso3 = fields[isoA3Index]?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(iso3))
                {
                    continue;
                }

                if (!latestByCountry.TryGetValue(iso3, out var existing) || existing.Date < rowDate)
                {
                    latestByCountry[iso3] = (rowDate, fields);
                }
            }

            if (latestByCountry.Count == 0)
            {
                return new ImportResult { Success = false, ErrorMessage = "No valid rows found in Big Mac source" };
            }

            var usFields = latestByCountry.GetValueOrDefault("USA").Fields;
            var usDollarPrice = 5.69m;
            if (usFields != null &&
                usFields.Length > dollarPriceIndex &&
                decimal.TryParse(usFields[dollarPriceIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedUsPrice) &&
                parsedUsPrice > 0)
            {
                usDollarPrice = parsedUsPrice;
            }

            var imported = 0;
            var updated = 0;
            var skipped = 0;
            DateTime? dataDate = null;

            foreach (var (_, (rowDate, fields)) in latestByCountry)
            {
                var iso3 = fields[isoA3Index]?.Trim().ToUpperInvariant();
                var regionCode = RegionCodeNormalizer.NormalizeToAlpha3(iso3);
                if (string.IsNullOrWhiteSpace(regionCode))
                {
                    skipped++;
                    continue;
                }

                if (!decimal.TryParse(fields[localPriceIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var localPrice) ||
                    !decimal.TryParse(fields[dollarPriceIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var dollarPrice) ||
                    dollarPrice <= 0)
                {
                    skipped++;
                    continue;
                }

                decimal? exchangeRate = null;
                if (dollarExIndex >= 0 &&
                    dollarExIndex < fields.Length &&
                    decimal.TryParse(fields[dollarExIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var ex))
                {
                    exchangeRate = ex;
                }

                var currencyCode = currencyIndex >= 0 && currencyIndex < fields.Length
                    ? fields[currencyIndex]?.Trim().ToUpperInvariant()
                    : null;
                var countryName = nameIndex >= 0 && nameIndex < fields.Length
                    ? fields[nameIndex]
                    : null;

                var usdRaw = 0m;
                var hasUsdRaw = usdRawIndex >= 0 &&
                                usdRawIndex < fields.Length &&
                                decimal.TryParse(fields[usdRawIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out usdRaw);
                var multiplier = hasUsdRaw
                    ? 1m + usdRaw
                    : (usDollarPrice > 0 ? dollarPrice / usDollarPrice : 1m);
                multiplier = Math.Clamp(multiplier, 0.1m, 3.0m);

                var existingRaw = await _context.PricingIndexRawData.FirstOrDefaultAsync(r =>
                    r.IndexType == PricingIndexType.BigMac &&
                    r.RegionCode == regionCode &&
                    r.PlanType == null);

                if (existingRaw == null)
                {
                    _context.PricingIndexRawData.Add(new PricingIndexRawData
                    {
                        Id = Guid.NewGuid(),
                        IndexType = PricingIndexType.BigMac,
                        RegionCode = regionCode,
                        CountryName = countryName,
                        CurrencyCode = currencyCode,
                        LocalPrice = localPrice,
                        UsdPrice = dollarPrice,
                        ExchangeRate = exchangeRate,
                        DataDate = rowDate,
                        ImportedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existingRaw.CountryName = countryName;
                    existingRaw.CurrencyCode = currencyCode;
                    existingRaw.LocalPrice = localPrice;
                    existingRaw.UsdPrice = dollarPrice;
                    existingRaw.ExchangeRate = exchangeRate;
                    existingRaw.DataDate = rowDate;
                    existingRaw.ImportedAt = DateTime.UtcNow;
                }

                var existingMultiplier = await _context.PppMultipliers.FirstOrDefaultAsync(m =>
                    m.RegionCode == regionCode &&
                    m.UserId == null &&
                    m.IndexType == PricingIndexType.BigMac &&
                    m.PlanType == null);

                if (existingMultiplier == null)
                {
                    _context.PppMultipliers.Add(new PppMultiplier
                    {
                        Id = Guid.NewGuid(),
                        RegionCode = regionCode,
                        CountryName = countryName,
                        Multiplier = multiplier,
                        Source = "big_mac_index",
                        CurrencyCode = currencyCode,
                        IndexType = PricingIndexType.BigMac,
                        PlanType = null,
                        DataDate = rowDate,
                        UserId = null,
                        UpdatedAt = DateTime.UtcNow
                    });
                    imported++;
                }
                else
                {
                    existingMultiplier.Multiplier = multiplier;
                    existingMultiplier.CountryName = countryName;
                    existingMultiplier.CurrencyCode = currencyCode;
                    existingMultiplier.Source = "big_mac_index";
                    existingMultiplier.DataDate = rowDate;
                    existingMultiplier.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }

                dataDate = dataDate.HasValue && dataDate.Value > rowDate ? dataDate : rowDate;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Big Mac import completed: {Imported} imported, {Updated} updated, {Skipped} skipped",
                imported, updated, skipped);

            return new ImportResult
            {
                Success = true,
                Imported = imported,
                Updated = updated,
                Total = imported + updated,
                DataDate = dataDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import Big Mac Index");
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImportResult> ImportNetflixIndexAsync(string? planType = null)
    {
        _logger.LogInformation("=== Starting Netflix Index import ===");
        _logger.LogInformation("URL: {Url}", NetflixJsonUrl);

        try
        {
            var response = await _httpClient.GetAsync(NetflixJsonUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new ImportResult { Success = false, ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var netflixData = JsonSerializer.Deserialize<List<NetflixCountryData>>(jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (netflixData == null || netflixData.Count == 0)
            {
                return new ImportResult { Success = false, ErrorMessage = "Netflix data is empty or invalid" };
            }

            var planTypesToImport = GetNetflixPlanTypes(planType);
            var normalizedUsCode = "USA";
            var imported = 0;
            var updated = 0;
            var skipped = 0;
            var dataDate = DateTime.UtcNow;

            foreach (var currentPlanType in planTypesToImport)
            {
                var usPrice = netflixData
                    .Where(c => RegionCodeNormalizer.NormalizeToAlpha3(c.CountryCode) == normalizedUsCode)
                    .SelectMany(c => c.Plans ?? [])
                    .Where(p => NormalizePlanType(p.Name) == currentPlanType && p.PriceUsd.HasValue && p.PriceUsd.Value > 0)
                    .Select(p => p.PriceUsd!.Value)
                    .FirstOrDefault();

                if (usPrice <= 0)
                {
                    _logger.LogWarning("Skipping Netflix plan {PlanType}: no valid US reference price", currentPlanType);
                    continue;
                }

                foreach (var entry in netflixData)
                {
                    var regionCode = RegionCodeNormalizer.NormalizeToAlpha3(entry.CountryCode);
                    if (string.IsNullOrWhiteSpace(regionCode))
                    {
                        skipped++;
                        continue;
                    }

                    var plan = entry.Plans?
                        .FirstOrDefault(p => NormalizePlanType(p.Name) == currentPlanType);

                    if (plan?.PriceUsd == null || plan.PriceUsd <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    var multiplier = Math.Clamp(plan.PriceUsd.Value / usPrice, 0.1m, 3.0m);
                    var currencyCode = entry.Currency?.Trim().ToUpperInvariant();

                    var existingRaw = await _context.PricingIndexRawData.FirstOrDefaultAsync(r =>
                        r.IndexType == PricingIndexType.Netflix &&
                        r.RegionCode == regionCode &&
                        r.PlanType == currentPlanType);

                    if (existingRaw == null)
                    {
                        _context.PricingIndexRawData.Add(new PricingIndexRawData
                        {
                            Id = Guid.NewGuid(),
                            IndexType = PricingIndexType.Netflix,
                            RegionCode = regionCode,
                            CountryName = entry.Country,
                            CurrencyCode = currencyCode,
                            LocalPrice = plan.Price ?? 0,
                            UsdPrice = plan.PriceUsd.Value,
                            PlanType = currentPlanType,
                            DataDate = dataDate,
                            ImportedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existingRaw.CountryName = entry.Country;
                        existingRaw.CurrencyCode = currencyCode;
                        existingRaw.LocalPrice = plan.Price ?? 0;
                        existingRaw.UsdPrice = plan.PriceUsd.Value;
                        existingRaw.PlanType = currentPlanType;
                        existingRaw.DataDate = dataDate;
                        existingRaw.ImportedAt = DateTime.UtcNow;
                    }

                    var existingMultiplier = await _context.PppMultipliers.FirstOrDefaultAsync(m =>
                        m.RegionCode == regionCode &&
                        m.UserId == null &&
                        m.IndexType == PricingIndexType.Netflix &&
                        m.PlanType == currentPlanType);

                    if (existingMultiplier == null)
                    {
                        _context.PppMultipliers.Add(new PppMultiplier
                        {
                            Id = Guid.NewGuid(),
                            RegionCode = regionCode,
                            CountryName = entry.Country,
                            Multiplier = multiplier,
                            Source = $"netflix_{currentPlanType}",
                            CurrencyCode = currencyCode,
                            IndexType = PricingIndexType.Netflix,
                            PlanType = currentPlanType,
                            DataDate = dataDate,
                            UserId = null,
                            UpdatedAt = DateTime.UtcNow
                        });
                        imported++;
                    }
                    else
                    {
                        existingMultiplier.Multiplier = multiplier;
                        existingMultiplier.CountryName = entry.Country;
                        existingMultiplier.CurrencyCode = currencyCode;
                        existingMultiplier.Source = $"netflix_{currentPlanType}";
                        existingMultiplier.DataDate = dataDate;
                        existingMultiplier.UpdatedAt = DateTime.UtcNow;
                        updated++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Netflix import completed: {Imported} imported, {Updated} updated, {Skipped} skipped",
                imported, updated, skipped);

            return new ImportResult
            {
                Success = true,
                Imported = imported,
                Updated = updated,
                Total = imported + updated,
                DataDate = dataDate
            };
        }
        catch (Exception ex)
        {
            var rootCause = UnwrapTypeInitializationException(ex);
            _logger.LogError(ex, "Failed to import Netflix Index");
            if (!ReferenceEquals(rootCause, ex))
            {
                _logger.LogError(
                    "Root cause for Netflix Index import failure: {ExceptionType}: {ExceptionMessage}",
                    rootCause.GetType().Name,
                    rootCause.Message);
            }

            return new ImportResult { Success = false, ErrorMessage = rootCause.Message };
        }
    }

    public async Task<ImportResult> ImportWageDataAsync()
    {
        _logger.LogInformation("=== Starting Wage Data import ===");

        try
        {
            var bigMacCurrencyByRegion = await _context.PricingIndexRawData
                .Where(r => r.IndexType == PricingIndexType.BigMac && r.PlanType == null)
                .ToDictionaryAsync(r => r.RegionCode, r => r.CurrencyCode);

            var imported = 0;
            var updated = 0;
            var dataDate = DateTime.UtcNow;

            foreach (var (alpha2Code, hourlyWage) in HourlyWagesUsd)
            {
                var regionCode = RegionCodeNormalizer.NormalizeToAlpha3(alpha2Code);
                if (string.IsNullOrWhiteSpace(regionCode))
                {
                    continue;
                }

                bigMacCurrencyByRegion.TryGetValue(regionCode, out var currencyCode);

                var existingRaw = await _context.PricingIndexRawData.FirstOrDefaultAsync(r =>
                    r.IndexType == PricingIndexType.BigMacWorkingHours &&
                    r.RegionCode == regionCode &&
                    r.PlanType == null);

                if (existingRaw == null)
                {
                    _context.PricingIndexRawData.Add(new PricingIndexRawData
                    {
                        Id = Guid.NewGuid(),
                        IndexType = PricingIndexType.BigMacWorkingHours,
                        RegionCode = regionCode,
                        CurrencyCode = currencyCode,
                        HourlyWage = hourlyWage,
                        DataDate = dataDate,
                        ImportedAt = DateTime.UtcNow
                    });
                    imported++;
                }
                else
                {
                    existingRaw.CurrencyCode = currencyCode;
                    existingRaw.HourlyWage = hourlyWage;
                    existingRaw.DataDate = dataDate;
                    existingRaw.ImportedAt = DateTime.UtcNow;
                    updated++;
                }
            }

            await _context.SaveChangesAsync();

            return new ImportResult
            {
                Success = true,
                Imported = imported,
                Updated = updated,
                Total = imported + updated,
                DataDate = dataDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import Wage Data");
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImportResult> CalculateBigMacWorkingHoursAsync()
    {
        _logger.LogInformation("=== Starting Big Mac Working Hours calculation ===");

        try
        {
            var bigMacData = await _context.PricingIndexRawData
                .Where(r => r.IndexType == PricingIndexType.BigMac && r.PlanType == null)
                .ToListAsync();

            if (bigMacData.Count == 0)
            {
                return new ImportResult { Success = false, ErrorMessage = "No Big Mac data available. Import Big Mac Index first." };
            }

            var usBigMac = bigMacData.FirstOrDefault(b => b.RegionCode == "USA")?.UsdPrice ?? 5.69m;
            var usWage = HourlyWagesUsd.GetValueOrDefault("US", 34.0m);
            var usWorkingHours = usBigMac / usWage;

            var imported = 0;
            var updated = 0;
            var skipped = 0;
            var dataDate = DateTime.UtcNow;

            foreach (var entry in bigMacData)
            {
                var alpha2Code = RegionCodeNormalizer.NormalizeToAlpha2(entry.RegionCode);
                if (string.IsNullOrWhiteSpace(alpha2Code) || !HourlyWagesUsd.TryGetValue(alpha2Code, out var hourlyWage))
                {
                    skipped++;
                    continue;
                }

                var workingHours = entry.UsdPrice / hourlyWage;
                var multiplier = Math.Clamp(workingHours / usWorkingHours, 0.1m, 5.0m);

                var existingMultiplier = await _context.PppMultipliers.FirstOrDefaultAsync(m =>
                    m.RegionCode == entry.RegionCode &&
                    m.UserId == null &&
                    m.IndexType == PricingIndexType.BigMacWorkingHours &&
                    m.PlanType == null);

                if (existingMultiplier == null)
                {
                    _context.PppMultipliers.Add(new PppMultiplier
                    {
                        Id = Guid.NewGuid(),
                        RegionCode = entry.RegionCode,
                        CountryName = entry.CountryName,
                        Multiplier = multiplier,
                        Source = "big_mac_working_hours",
                        CurrencyCode = entry.CurrencyCode,
                        IndexType = PricingIndexType.BigMacWorkingHours,
                        PlanType = null,
                        DataDate = dataDate,
                        UserId = null,
                        UpdatedAt = DateTime.UtcNow
                    });
                    imported++;
                }
                else
                {
                    existingMultiplier.Multiplier = multiplier;
                    existingMultiplier.CountryName = entry.CountryName;
                    existingMultiplier.CurrencyCode = entry.CurrencyCode;
                    existingMultiplier.Source = "big_mac_working_hours";
                    existingMultiplier.DataDate = dataDate;
                    existingMultiplier.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Working hours calculation completed: {Imported} imported, {Updated} updated, {Skipped} skipped",
                imported, updated, skipped);

            return new ImportResult
            {
                Success = true,
                Imported = imported,
                Updated = updated,
                Total = imported + updated,
                DataDate = dataDate
            };
        }
        catch (Exception ex)
        {
            var rootCause = UnwrapTypeInitializationException(ex);
            _logger.LogError(ex, "Failed to calculate Big Mac Working Hours");
            if (!ReferenceEquals(rootCause, ex))
            {
                _logger.LogError(
                    "Root cause for Big Mac Working Hours failure: {ExceptionType}: {ExceptionMessage}",
                    rootCause.GetType().Name,
                    rootCause.Message);
            }

            return new ImportResult { Success = false, ErrorMessage = rootCause.Message };
        }
    }

    private static Exception UnwrapTypeInitializationException(Exception ex)
    {
        if (ex is not TypeInitializationException)
        {
            return ex;
        }

        var current = ex;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current;
    }

    private static IReadOnlyCollection<string> GetNetflixPlanTypes(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType) || string.Equals(planType.Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            return SupportedNetflixPlans;
        }

        var normalized = NormalizePlanType(planType);
        if (normalized == null || !SupportedNetflixPlans.Contains(normalized))
        {
            return ["standard"];
        }

        return [normalized];
    }

    private static string? NormalizePlanType(string? planType)
    {
        return string.IsNullOrWhiteSpace(planType)
            ? null
            : planType.Trim().ToLowerInvariant();
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Add('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(new string(current.ToArray()).Trim());
                current.Clear();
                continue;
            }

            current.Add(c);
        }

        result.Add(new string(current.ToArray()).Trim());
        return result.ToArray();
    }
}

public class ImportResult
{
    public bool Success { get; set; }
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Total { get; set; }
    public DateTime? DataDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public class NetflixCountryData
{
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("plans")]
    public List<NetflixPlan>? Plans { get; set; }
}

public class NetflixPlan
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("price_usd")]
    public decimal? PriceUsd { get; set; }
}
