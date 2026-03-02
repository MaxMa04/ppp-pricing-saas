namespace PppPricing.API.Services;

public static class CurrencyConversion
{
    // Currency units per USD.
    private static readonly Dictionary<string, decimal> RatesToUsd = new(StringComparer.OrdinalIgnoreCase)
    {
        { "USD", 1.0m }, { "EUR", 0.92m }, { "GBP", 0.79m }, { "JPY", 149.5m },
        { "CAD", 1.36m }, { "AUD", 1.53m }, { "CHF", 0.88m }, { "CNY", 7.24m },
        { "INR", 83.4m }, { "BRL", 4.97m }, { "MXN", 17.15m }, { "ZAR", 18.6m },
        { "KRW", 1330m }, { "SGD", 1.34m }, { "HKD", 7.82m }, { "SEK", 10.5m },
        { "NOK", 10.7m }, { "DKK", 6.87m }, { "PLN", 4.0m }, { "TRY", 32.5m },
        { "RUB", 92m }, { "THB", 35.5m }, { "IDR", 15700m }, { "MYR", 4.72m },
        { "PHP", 56.2m }, { "VND", 24500m }, { "AED", 3.67m }, { "SAR", 3.75m },
        { "ILS", 3.7m }, { "EGP", 30.9m }, { "NGN", 1550m }, { "KES", 157m },
        { "CLP", 950m }, { "COP", 4000m }, { "ARS", 870m }, { "PEN", 3.72m },
        { "NZD", 1.64m }, { "TWD", 31.8m },
    };

    public static decimal Convert(decimal amount, string fromCurrency, string toCurrency)
    {
        var source = string.IsNullOrWhiteSpace(fromCurrency) ? "USD" : fromCurrency.Trim().ToUpperInvariant();
        var target = string.IsNullOrWhiteSpace(toCurrency) ? "USD" : toCurrency.Trim().ToUpperInvariant();

        if (source == target)
        {
            return amount;
        }

        if (!RatesToUsd.TryGetValue(source, out var sourceRate) ||
            !RatesToUsd.TryGetValue(target, out var targetRate) ||
            sourceRate <= 0)
        {
            return amount;
        }

        var amountInUsd = amount / sourceRate;
        return amountInUsd * targetRate;
    }
}
