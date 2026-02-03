using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;

namespace PppPricing.API.Services;

public interface IPricingIndexImportService
{
    Task<ImportResult> ImportBigMacIndexAsync();
    Task<ImportResult> ImportNetflixIndexAsync(string planType = "standard");
    Task<ImportResult> ImportWageDataAsync();
    Task<ImportResult> CalculateBigMacWorkingHoursAsync();
}

public class PricingIndexImportService : IPricingIndexImportService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PricingIndexImportService> _logger;

    private const string BigMacCsvUrl = "https://raw.githubusercontent.com/TheEconomist/big-mac-data/master/output-data/big-mac-full-index.csv";
    private const string NetflixJsonUrl = "https://raw.githubusercontent.com/tompec/netflix-prices/main/data/prices.json";

    // ISO 3166-1 alpha-3 to alpha-2 mapping for Big Mac data
    private static readonly Dictionary<string, string> Alpha3ToAlpha2 = new()
    {
        { "ARG", "AR" }, { "AUS", "AU" }, { "AUT", "AT" }, { "AZE", "AZ" },
        { "BHR", "BH" }, { "BRA", "BR" }, { "GBR", "GB" }, { "CAN", "CA" },
        { "CHL", "CL" }, { "CHN", "CN" }, { "COL", "CO" }, { "CRI", "CR" },
        { "HRV", "HR" }, { "CZE", "CZ" }, { "DNK", "DK" }, { "EGY", "EG" },
        { "EUZ", "EU" }, { "GTM", "GT" }, { "HND", "HN" }, { "HKG", "HK" },
        { "HUN", "HU" }, { "IND", "IN" }, { "IDN", "ID" }, { "ISR", "IL" },
        { "JPN", "JP" }, { "JOR", "JO" }, { "KWT", "KW" }, { "LBN", "LB" },
        { "MYS", "MY" }, { "MEX", "MX" }, { "MDA", "MD" }, { "NIC", "NI" },
        { "NOR", "NO" }, { "OMN", "OM" }, { "PAK", "PK" }, { "PER", "PE" },
        { "PHL", "PH" }, { "POL", "PL" }, { "QAT", "QA" }, { "ROU", "RO" },
        { "RUS", "RU" }, { "SAU", "SA" }, { "SGP", "SG" }, { "ZAF", "ZA" },
        { "KOR", "KR" }, { "LKA", "LK" }, { "SWE", "SE" }, { "CHE", "CH" },
        { "TWN", "TW" }, { "THA", "TH" }, { "TUR", "TR" }, { "ARE", "AE" },
        { "UKR", "UA" }, { "URY", "UY" }, { "USA", "US" }, { "VEN", "VE" },
        { "VNM", "VN" }, { "NZL", "NZ" },
    };

    // Average hourly wages in USD (approximation based on GDP per capita and average work hours)
    private static readonly Dictionary<string, decimal> HourlyWagesUsd = new()
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
        _logger.LogInformation("Timestamp: {Timestamp}", DateTime.UtcNow);

        try
        {
            _logger.LogInformation("Fetching CSV data from GitHub...");
            var response = await _httpClient.GetAsync(BigMacCsvUrl);
            _logger.LogInformation("HTTP Response: Status={StatusCode}, ReasonPhrase={ReasonPhrase}",
                (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch CSV: HTTP {StatusCode}", (int)response.StatusCode);
                return new ImportResult { Success = false, ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }

            var csvContent = await response.Content.ReadAsStringAsync();
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            _logger.LogInformation("CSV fetched successfully: {ByteLength} bytes, {LineCount} lines",
                csvContent.Length, lines.Length);

            if (lines.Length < 2)
            {
                _logger.LogWarning("CSV file is empty or contains only header");
                return new ImportResult { Success = false, ErrorMessage = "CSV file is empty or invalid" };
            }

            // Parse header
            var header = lines[0].Split(',');
            var dateIndex = Array.IndexOf(header, "date");
            var isoA3Index = Array.IndexOf(header, "iso_a3");
            var nameIndex = Array.IndexOf(header, "name");
            var localPriceIndex = Array.IndexOf(header, "local_price");
            var dollarPriceIndex = Array.IndexOf(header, "dollar_price");
            var dollarExIndex = Array.IndexOf(header, "dollar_ex");
            var usdRawIndex = Array.IndexOf(header, "USD_raw");

            _logger.LogInformation("CSV Header columns: {ColumnCount}", header.Length);
            _logger.LogInformation("Header indices - date:{DateIdx}, iso_a3:{IsoIdx}, name:{NameIdx}, local_price:{LocalIdx}, dollar_price:{DollarIdx}, dollar_ex:{ExIdx}, USD_raw:{RawIdx}",
                dateIndex, isoA3Index, nameIndex, localPriceIndex, dollarPriceIndex, dollarExIndex, usdRawIndex);

            if (dateIndex < 0 || isoA3Index < 0 || dollarPriceIndex < 0)
            {
                _logger.LogError("Required columns not found in CSV header. Header: {Header}", lines[0]);
                return new ImportResult { Success = false, ErrorMessage = "Required columns not found in CSV" };
            }

            // Group by country and get latest entry for each
            var latestByCountry = new Dictionary<string, (DateTime Date, string[] Fields)>();
            var skippedRows = 0;
            var parsedRows = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = ParseCsvLine(lines[i]);
                if (fields.Length <= Math.Max(dateIndex, Math.Max(isoA3Index, dollarPriceIndex)))
                {
                    skippedRows++;
                    continue;
                }

                if (!DateTime.TryParse(fields[dateIndex], out var date))
                {
                    _logger.LogDebug("Row {Row}: Failed to parse date '{DateValue}'", i, fields[dateIndex]);
                    skippedRows++;
                    continue;
                }

                var iso3 = fields[isoA3Index];
                if (string.IsNullOrEmpty(iso3))
                {
                    skippedRows++;
                    continue;
                }

                parsedRows++;
                if (!latestByCountry.TryGetValue(iso3, out var existing) || existing.Date < date)
                {
                    latestByCountry[iso3] = (date, fields);
                }
            }

            _logger.LogInformation("CSV parsing complete: {ParsedRows} rows parsed, {SkippedRows} rows skipped, {UniqueCountries} unique countries",
                parsedRows, skippedRows, latestByCountry.Count);

            var imported = 0;
            var updated = 0;
            var skippedNoMapping = 0;
            var skippedParseFailed = 0;
            DateTime? dataDate = null;

            // Get US price first for reference
            var usFields = latestByCountry.GetValueOrDefault("USA").Fields;
            decimal usaDollarPrice = 5.69m;
            if (usFields != null && decimal.TryParse(usFields[dollarPriceIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var usp))
            {
                usaDollarPrice = usp;
            }
            _logger.LogInformation("US Big Mac reference price: ${UsdPrice:F2}", usaDollarPrice);

            foreach (var (iso3, (date, fields)) in latestByCountry)
            {
                // Convert ISO-3 to ISO-2
                if (!Alpha3ToAlpha2.TryGetValue(iso3, out var regionCode))
                {
                    _logger.LogDebug("Skipping {Iso3}: No ISO-2 mapping found in Alpha3ToAlpha2 dictionary", iso3);
                    skippedNoMapping++;
                    continue;
                }

                if (!decimal.TryParse(fields[localPriceIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var localPrice))
                {
                    _logger.LogWarning("Skipping {Iso3} -> {RegionCode}: Failed to parse local_price '{Value}'",
                        iso3, regionCode, fields[localPriceIndex]);
                    skippedParseFailed++;
                    continue;
                }
                if (!decimal.TryParse(fields[dollarPriceIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var dollarPrice))
                {
                    _logger.LogWarning("Skipping {Iso3} -> {RegionCode}: Failed to parse dollar_price '{Value}'",
                        iso3, regionCode, fields[dollarPriceIndex]);
                    skippedParseFailed++;
                    continue;
                }

                decimal? exchangeRate = null;
                if (dollarExIndex >= 0 && dollarExIndex < fields.Length)
                {
                    if (decimal.TryParse(fields[dollarExIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var ex))
                    {
                        exchangeRate = ex;
                    }
                }

                decimal usdRaw = 0;
                if (usdRawIndex >= 0 && usdRawIndex < fields.Length)
                    decimal.TryParse(fields[usdRawIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out usdRaw);

                var countryName = nameIndex >= 0 && nameIndex < fields.Length ? fields[nameIndex] : null;
                dataDate ??= date;

                // Calculate multiplier
                var multiplier = usdRaw != 0 ? (1 + usdRaw / 100) : (dollarPrice / usaDollarPrice);
                var originalMultiplier = multiplier;
                multiplier = Math.Clamp(multiplier, 0.1m, 3.0m);

                _logger.LogDebug("Processing {Iso3} -> {RegionCode} ({Country}): local={LocalPrice:F2}, usd=${DollarPrice:F2}, usdRaw={UsdRaw:F2}%, multiplier={Multiplier:F4} (clamped from {Original:F4})",
                    iso3, regionCode, countryName ?? "Unknown", localPrice, dollarPrice, usdRaw, multiplier, originalMultiplier);

                // Save raw data
                var existingRaw = await _context.PricingIndexRawData
                    .FirstOrDefaultAsync(r => r.IndexType == PricingIndexType.BigMac && r.RegionCode == regionCode);

                if (existingRaw == null)
                {
                    _context.PricingIndexRawData.Add(new PricingIndexRawData
                    {
                        Id = Guid.NewGuid(),
                        IndexType = PricingIndexType.BigMac,
                        RegionCode = regionCode,
                        CountryName = countryName,
                        LocalPrice = localPrice,
                        UsdPrice = dollarPrice,
                        ExchangeRate = exchangeRate,
                        DataDate = date,
                        ImportedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existingRaw.CountryName = countryName;
                    existingRaw.LocalPrice = localPrice;
                    existingRaw.UsdPrice = dollarPrice;
                    existingRaw.ExchangeRate = exchangeRate;
                    existingRaw.DataDate = date;
                    existingRaw.ImportedAt = DateTime.UtcNow;
                }

                // Save multiplier
                var existingMultiplier = await _context.PppMultipliers
                    .FirstOrDefaultAsync(m => m.RegionCode == regionCode &&
                                             m.UserId == null &&
                                             m.IndexType == PricingIndexType.BigMac);

                if (existingMultiplier == null)
                {
                    _context.PppMultipliers.Add(new PppMultiplier
                    {
                        Id = Guid.NewGuid(),
                        RegionCode = regionCode,
                        CountryName = countryName,
                        Multiplier = multiplier,
                        Source = "big_mac_index",
                        IndexType = PricingIndexType.BigMac,
                        DataDate = date,
                        UserId = null,
                        UpdatedAt = DateTime.UtcNow
                    });
                    imported++;
                    _logger.LogDebug("Created new multiplier for {RegionCode}", regionCode);
                }
                else
                {
                    var previousMultiplier = existingMultiplier.Multiplier;
                    existingMultiplier.Multiplier = multiplier;
                    existingMultiplier.CountryName = countryName;
                    existingMultiplier.DataDate = date;
                    existingMultiplier.UpdatedAt = DateTime.UtcNow;
                    updated++;
                    _logger.LogDebug("Updated multiplier for {RegionCode}: {OldValue:F4} -> {NewValue:F4}",
                        regionCode, previousMultiplier, multiplier);
                }
            }

            _logger.LogInformation("Saving changes to database...");
            await _context.SaveChangesAsync();

            _logger.LogInformation("=== Big Mac Index import completed ===");
            _logger.LogInformation("Results: {Imported} new, {Updated} updated, {Total} total",
                imported, updated, imported + updated);
            _logger.LogInformation("Skipped: {NoMapping} (no ISO-2 mapping), {ParseFailed} (parse failures)",
                skippedNoMapping, skippedParseFailed);
            _logger.LogInformation("Data date: {DataDate}", dataDate);

            return new ImportResult
            {
                Success = true,
                Imported = imported,
                Updated = updated,
                Total = imported + updated,
                DataDate = dataDate
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for Big Mac Index import: {Message}", ex.Message);
            return new ImportResult { Success = false, ErrorMessage = $"HTTP Error: {ex.Message}" };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout during Big Mac Index import");
            return new ImportResult { Success = false, ErrorMessage = "Request timeout" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import Big Mac Index. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImportResult> ImportNetflixIndexAsync(string planType = "standard")
    {
        _logger.LogInformation("=== Starting Netflix Index import ===");
        _logger.LogInformation("URL: {Url}", NetflixJsonUrl);
        _logger.LogInformation("Plan type: {PlanType}", planType);
        _logger.LogInformation("Timestamp: {Timestamp}", DateTime.UtcNow);

        try
        {
            _logger.LogInformation("Fetching JSON data from GitHub...");
            var response = await _httpClient.GetAsync(NetflixJsonUrl);
            _logger.LogInformation("HTTP Response: Status={StatusCode}, ReasonPhrase={ReasonPhrase}",
                (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch JSON: HTTP {StatusCode}", (int)response.StatusCode);
                return new ImportResult { Success = false, ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("JSON fetched successfully: {ByteLength} bytes", jsonContent.Length);

            var netflixData = JsonSerializer.Deserialize<List<NetflixCountryData>>(jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (netflixData == null || netflixData.Count == 0)
            {
                _logger.LogWarning("Netflix data is empty or failed to deserialize");
                return new ImportResult { Success = false, ErrorMessage = "Netflix data is empty or invalid" };
            }

            _logger.LogInformation("Deserialized {Count} countries from Netflix data", netflixData.Count);

            // Find US price for reference
            var usEntry = netflixData.FirstOrDefault(n => n.CountryCode?.ToUpper() == "US");
            var usPrice = usEntry?.Plans?.FirstOrDefault(p => p.Name?.ToLower() == planType.ToLower())?.PriceUsd ?? 15.49m;
            _logger.LogInformation("US Netflix {PlanType} reference price: ${UsdPrice:F2}", planType, usPrice);

            var imported = 0;
            var updated = 0;
            var skippedNoCode = 0;
            var skippedNoPlan = 0;
            var dataDate = DateTime.UtcNow;

            foreach (var entry in netflixData)
            {
                if (string.IsNullOrEmpty(entry.CountryCode))
                {
                    _logger.LogDebug("Skipping entry with no country code: {Country}", entry.Country);
                    skippedNoCode++;
                    continue;
                }

                var regionCode = entry.CountryCode.ToUpper();
                var plan = entry.Plans?.FirstOrDefault(p => p.Name?.ToLower() == planType.ToLower());

                if (plan == null || plan.PriceUsd == null || plan.PriceUsd <= 0)
                {
                    _logger.LogDebug("Skipping {RegionCode} ({Country}): No valid {PlanType} plan found",
                        regionCode, entry.Country, planType);
                    skippedNoPlan++;
                    continue;
                }

                // Calculate multiplier
                var multiplier = plan.PriceUsd.Value / usPrice;
                var originalMultiplier = multiplier;
                multiplier = Math.Clamp(multiplier, 0.1m, 3.0m);

                _logger.LogDebug("Processing {RegionCode} ({Country}): local={LocalPrice:F2} {Currency}, usd=${UsdPrice:F2}, multiplier={Multiplier:F4}",
                    regionCode, entry.Country, plan.Price ?? 0, entry.Currency ?? "?", plan.PriceUsd.Value, multiplier);

                // Save raw data
                var existingRaw = await _context.PricingIndexRawData
                    .FirstOrDefaultAsync(r => r.IndexType == PricingIndexType.Netflix &&
                                             r.RegionCode == regionCode &&
                                             r.PlanType == planType);

                if (existingRaw == null)
                {
                    _context.PricingIndexRawData.Add(new PricingIndexRawData
                    {
                        Id = Guid.NewGuid(),
                        IndexType = PricingIndexType.Netflix,
                        RegionCode = regionCode,
                        CountryName = entry.Country,
                        CurrencyCode = entry.Currency,
                        LocalPrice = plan.Price ?? 0,
                        UsdPrice = plan.PriceUsd.Value,
                        PlanType = planType,
                        DataDate = dataDate,
                        ImportedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existingRaw.CountryName = entry.Country;
                    existingRaw.CurrencyCode = entry.Currency;
                    existingRaw.LocalPrice = plan.Price ?? 0;
                    existingRaw.UsdPrice = plan.PriceUsd.Value;
                    existingRaw.DataDate = dataDate;
                    existingRaw.ImportedAt = DateTime.UtcNow;
                }

                // Save multiplier
                var existingMultiplier = await _context.PppMultipliers
                    .FirstOrDefaultAsync(m => m.RegionCode == regionCode &&
                                             m.UserId == null &&
                                             m.IndexType == PricingIndexType.Netflix);

                if (existingMultiplier == null)
                {
                    _context.PppMultipliers.Add(new PppMultiplier
                    {
                        Id = Guid.NewGuid(),
                        RegionCode = regionCode,
                        CountryName = entry.Country,
                        Multiplier = multiplier,
                        Source = $"netflix_{planType}",
                        IndexType = PricingIndexType.Netflix,
                        DataDate = dataDate,
                        UserId = null,
                        UpdatedAt = DateTime.UtcNow
                    });
                    imported++;
                    _logger.LogDebug("Created new multiplier for {RegionCode}", regionCode);
                }
                else
                {
                    var previousMultiplier = existingMultiplier.Multiplier;
                    existingMultiplier.Multiplier = multiplier;
                    existingMultiplier.CountryName = entry.Country;
                    existingMultiplier.DataDate = dataDate;
                    existingMultiplier.UpdatedAt = DateTime.UtcNow;
                    updated++;
                    _logger.LogDebug("Updated multiplier for {RegionCode}: {OldValue:F4} -> {NewValue:F4}",
                        regionCode, previousMultiplier, multiplier);
                }
            }

            _logger.LogInformation("Saving changes to database...");
            await _context.SaveChangesAsync();

            _logger.LogInformation("=== Netflix Index import completed ===");
            _logger.LogInformation("Results: {Imported} new, {Updated} updated, {Total} total",
                imported, updated, imported + updated);
            _logger.LogInformation("Skipped: {NoCode} (no country code), {NoPlan} (no valid plan)",
                skippedNoCode, skippedNoPlan);

            return new ImportResult
            {
                Success = true,
                Imported = imported,
                Updated = updated,
                Total = imported + updated,
                DataDate = dataDate
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for Netflix Index import: {Message}", ex.Message);
            return new ImportResult { Success = false, ErrorMessage = $"HTTP Error: {ex.Message}" };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing failed for Netflix Index import: {Message}", ex.Message);
            return new ImportResult { Success = false, ErrorMessage = $"JSON Error: {ex.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import Netflix Index. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImportResult> ImportWageDataAsync()
    {
        _logger.LogInformation("=== Starting Wage Data import ===");
        _logger.LogInformation("Source: Pre-defined hourly wages dictionary");
        _logger.LogInformation("Timestamp: {Timestamp}", DateTime.UtcNow);
        _logger.LogInformation("Countries in wage dictionary: {Count}", HourlyWagesUsd.Count);

        try
        {
            var imported = 0;
            var updated = 0;
            var dataDate = DateTime.UtcNow;

            foreach (var (regionCode, hourlyWage) in HourlyWagesUsd)
            {
                _logger.LogDebug("Processing {RegionCode}: hourlyWage=${HourlyWage:F2}/hour", regionCode, hourlyWage);

                var existingRaw = await _context.PricingIndexRawData
                    .FirstOrDefaultAsync(r => r.IndexType == PricingIndexType.BigMacWorkingHours &&
                                             r.RegionCode == regionCode);

                if (existingRaw == null)
                {
                    _context.PricingIndexRawData.Add(new PricingIndexRawData
                    {
                        Id = Guid.NewGuid(),
                        IndexType = PricingIndexType.BigMacWorkingHours,
                        RegionCode = regionCode,
                        HourlyWage = hourlyWage,
                        DataDate = dataDate,
                        ImportedAt = DateTime.UtcNow
                    });
                    imported++;
                    _logger.LogDebug("Created new wage entry for {RegionCode}", regionCode);
                }
                else
                {
                    var previousWage = existingRaw.HourlyWage;
                    existingRaw.HourlyWage = hourlyWage;
                    existingRaw.DataDate = dataDate;
                    existingRaw.ImportedAt = DateTime.UtcNow;
                    updated++;
                    _logger.LogDebug("Updated wage entry for {RegionCode}: ${OldValue:F2} -> ${NewValue:F2}",
                        regionCode, previousWage, hourlyWage);
                }
            }

            _logger.LogInformation("Saving changes to database...");
            await _context.SaveChangesAsync();

            _logger.LogInformation("=== Wage Data import completed ===");
            _logger.LogInformation("Results: {Imported} new, {Updated} updated, {Total} total",
                imported, updated, imported + updated);

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
            _logger.LogError(ex, "Failed to import Wage Data. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImportResult> CalculateBigMacWorkingHoursAsync()
    {
        _logger.LogInformation("=== Starting Big Mac Working Hours calculation ===");
        _logger.LogInformation("Timestamp: {Timestamp}", DateTime.UtcNow);

        try
        {
            // First ensure we have Big Mac data
            _logger.LogInformation("Loading Big Mac raw data from database...");
            var bigMacData = await _context.PricingIndexRawData
                .Where(r => r.IndexType == PricingIndexType.BigMac)
                .ToListAsync();

            _logger.LogInformation("Found {Count} Big Mac entries in database", bigMacData.Count);

            if (bigMacData.Count == 0)
            {
                _logger.LogWarning("No Big Mac data available. Import Big Mac Index first.");
                return new ImportResult { Success = false, ErrorMessage = "No Big Mac data available. Import Big Mac Index first." };
            }

            var imported = 0;
            var updated = 0;
            var skippedNoWage = 0;
            var dataDate = DateTime.UtcNow;

            // Get US reference data
            var usWage = HourlyWagesUsd.GetValueOrDefault("US", 34.0m);
            var usBigMac = bigMacData.FirstOrDefault(b => b.RegionCode == "US")?.UsdPrice ?? 5.69m;
            var usWorkingHours = usBigMac / usWage;

            _logger.LogInformation("US Reference: wage=${UsWage:F2}/hour, Big Mac=${UsBigMac:F2}, working hours={UsWorkingHours:F4}h",
                usWage, usBigMac, usWorkingHours);

            foreach (var entry in bigMacData)
            {
                if (!HourlyWagesUsd.TryGetValue(entry.RegionCode, out var hourlyWage))
                {
                    _logger.LogDebug("Skipping {RegionCode} ({Country}): No wage data in HourlyWagesUsd dictionary",
                        entry.RegionCode, entry.CountryName);
                    skippedNoWage++;
                    continue;
                }

                // Working hours to buy a Big Mac
                var workingHours = entry.UsdPrice / hourlyWage;
                var multiplier = workingHours / usWorkingHours;
                var originalMultiplier = multiplier;
                multiplier = Math.Clamp(multiplier, 0.1m, 5.0m);

                _logger.LogDebug("Processing {RegionCode} ({Country}): wage=${Wage:F2}/hour, bigMac=${BigMac:F2}, workingHours={WorkingHours:F4}h, multiplier={Multiplier:F4}",
                    entry.RegionCode, entry.CountryName, hourlyWage, entry.UsdPrice, workingHours, multiplier);

                // Update raw data with working hours
                entry.HourlyWage = hourlyWage;
                entry.WorkingHours = workingHours;

                // Save multiplier
                var existingMultiplier = await _context.PppMultipliers
                    .FirstOrDefaultAsync(m => m.RegionCode == entry.RegionCode &&
                                             m.UserId == null &&
                                             m.IndexType == PricingIndexType.BigMacWorkingHours);

                if (existingMultiplier == null)
                {
                    _context.PppMultipliers.Add(new PppMultiplier
                    {
                        Id = Guid.NewGuid(),
                        RegionCode = entry.RegionCode,
                        CountryName = entry.CountryName,
                        Multiplier = multiplier,
                        Source = "big_mac_working_hours",
                        IndexType = PricingIndexType.BigMacWorkingHours,
                        DataDate = dataDate,
                        UserId = null,
                        UpdatedAt = DateTime.UtcNow
                    });
                    imported++;
                    _logger.LogDebug("Created new working hours multiplier for {RegionCode}", entry.RegionCode);
                }
                else
                {
                    var previousMultiplier = existingMultiplier.Multiplier;
                    existingMultiplier.Multiplier = multiplier;
                    existingMultiplier.CountryName = entry.CountryName;
                    existingMultiplier.DataDate = dataDate;
                    existingMultiplier.UpdatedAt = DateTime.UtcNow;
                    updated++;
                    _logger.LogDebug("Updated working hours multiplier for {RegionCode}: {OldValue:F4} -> {NewValue:F4}",
                        entry.RegionCode, previousMultiplier, multiplier);
                }
            }

            _logger.LogInformation("Saving changes to database...");
            await _context.SaveChangesAsync();

            _logger.LogInformation("=== Big Mac Working Hours calculation completed ===");
            _logger.LogInformation("Results: {Imported} new, {Updated} updated, {Total} total",
                imported, updated, imported + updated);
            _logger.LogInformation("Skipped: {NoWage} (no wage data available)", skippedNoWage);

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
            _logger.LogError(ex, "Failed to calculate Big Mac Working Hours. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = "";

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current.Trim());

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
    public string? CountryCode { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public List<NetflixPlan>? Plans { get; set; }
}

public class NetflixPlan
{
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public decimal? PriceUsd { get; set; }
}
