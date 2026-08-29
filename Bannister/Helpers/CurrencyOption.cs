namespace Bannister.Helpers;

public record CurrencyOption(string Code, string Symbol, string Label)
{
    public static readonly List<CurrencyOption> All = new()
    {
        new("USD", "$",  "USD – US Dollar"),
        new("ILS", "₪",  "ILS – Israeli Shekel"),
        new("EUR", "€",  "EUR – Euro"),
        new("GBP", "£",  "GBP – British Pound"),
        new("JPY", "¥",  "JPY – Japanese Yen"),
        new("CAD", "CA$","CAD – Canadian Dollar"),
        new("AUD", "A$", "AUD – Australian Dollar"),
        new("CHF", "Fr", "CHF – Swiss Franc"),
        new("CNY", "¥",  "CNY – Chinese Yuan"),
        new("INR", "₹",  "INR – Indian Rupee"),
        new("MXN", "MX$","MXN – Mexican Peso"),
        new("BRL", "R$", "BRL – Brazilian Real"),
        new("KRW", "₩",  "KRW – South Korean Won"),
        new("SGD", "S$", "SGD – Singapore Dollar"),
        new("HKD", "HK$","HKD – Hong Kong Dollar"),
        new("NOK", "kr", "NOK – Norwegian Krone"),
        new("SEK", "kr", "SEK – Swedish Krona"),
        new("DKK", "kr", "DKK – Danish Krone"),
        new("NZD", "NZ$","NZD – New Zealand Dollar"),
        new("ZAR", "R",  "ZAR – South African Rand"),
    };

    public static CurrencyOption Default => All[0];

    public static CurrencyOption FromCode(string code) =>
        All.FirstOrDefault(c => c.Code == code) ?? Default;
}
