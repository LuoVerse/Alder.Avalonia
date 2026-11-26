using System.Globalization;

namespace Alder.Avalonia.Controls;

internal static class NumberValueFormatter
{
    private const string NumberMarker = "1";
    private static readonly Dictionary<string, (string Symbol, int Digits)> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = ("$", 2), ["EUR"] = ("€", 2), ["GBP"] = ("£", 2), ["JPY"] = ("¥", 0), ["CNY"] = ("¥", 2),
        ["CAD"] = ("$", 2), ["AUD"] = ("$", 2), ["NZD"] = ("$", 2), ["HKD"] = ("$", 2), ["SGD"] = ("$", 2),
        ["KRW"] = ("₩", 0), ["INR"] = ("₹", 2), ["CHF"] = ("CHF", 2), ["BRL"] = ("R$", 2), ["MXN"] = ("$", 2),
        ["RUB"] = ("₽", 2), ["TRY"] = ("₺", 2), ["ILS"] = ("₪", 2), ["THB"] = ("฿", 2), ["VND"] = ("₫", 0),
        ["IDR"] = ("Rp", 2), ["CLP"] = ("$", 0), ["ISK"] = ("kr", 0), ["KWD"] = ("KWD", 3),
        ["BHD"] = ("BHD", 3), ["OMR"] = ("OMR", 3), ["JOD"] = ("JOD", 3), ["TND"] = ("TND", 3), ["XAF"] = ("FCFA", 0),
        ["XOF"] = ("CFA", 0), ["XPF"] = ("CFPF", 0), ["SEK"] = ("kr", 2), ["NOK"] = ("kr", 2), ["DKK"] = ("kr", 2)
    };

    private static readonly Dictionary<string, string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["degree"] = "°", ["celsius"] = "°C", ["fahrenheit"] = "°F", ["percent"] = "%",
        ["millimeter"] = " mm", ["centimeter"] = " cm", ["meter"] = " m", ["kilometer"] = " km",
        ["inch"] = " in", ["foot"] = " ft", ["yard"] = " yd", ["mile"] = " mi",
        ["gram"] = " g", ["kilogram"] = " kg", ["ounce"] = " oz", ["pound"] = " lb",
        ["milliliter"] = " mL", ["liter"] = " L", ["gallon"] = " gal",
        ["millisecond"] = " ms", ["second"] = " s", ["minute"] = " min", ["hour"] = " hr",
        ["day"] = " days", ["week"] = " wks", ["month"] = " mths", ["year"] = " yrs",
        ["bit"] = " bit", ["byte"] = " byte", ["kilobyte"] = " kB", ["megabyte"] = " MB",
        ["gigabyte"] = " GB", ["terabyte"] = " TB", ["petabyte"] = " PB"
    };

    internal static string Format(AlderNumberValue control, CultureInfo culture)
    {
        var options = control.FormatOptions;
        var style = options?.Style ?? control.Style;
        var notation = options?.Notation ?? control.Notation;
        var sign = options?.SignDisplay ?? control.SignDisplay;
        var info = culture.NumberFormat;
        var currency = style == NumberValueStyle.Currency
            ? Currency(culture, options?.Currency ?? control.Currency, options?.CurrencyDisplay ?? default)
            : default;
        double input = control.Value;
        if (double.IsNaN(input)) return info.NaNSymbol;

        double magnitude = Math.Abs(input);
        if (style == NumberValueStyle.Percent) magnitude *= 100;
        int defaultMin = style == NumberValueStyle.Currency && notation != NumberValueNotation.Compact ? currency.Digits : 0;
        int defaultMax = style == NumberValueStyle.Currency ? defaultMin : style == NumberValueStyle.Percent ? 0 : 3;
        var minOption = options?.MinimumFractionDigits ?? control.MinimumFractionDigits;
        var maxOption = options?.MaximumFractionDigits ?? control.MaximumFractionDigits;
        int min = Math.Clamp(minOption ?? Math.Min(defaultMin, maxOption ?? 20), 0, 20);
        int max = Math.Clamp(maxOption ?? Math.Max(defaultMax, min), min, 20);

        var (scaled, ending) = Scale(magnitude, notation, culture);
        if (notation == NumberValueNotation.Compact && ending.Length > 0 && minOption is null && maxOption is null)
            max = scaled < 10 ? 1 : 0;

        if (double.IsFinite(scaled) && scaled > 0 && options is not null &&
            (options.MinimumSignificantDigits.HasValue || options.MaximumSignificantDigits.HasValue))
        {
            var integerDigits = 1 + (int)Math.Floor(Math.Log10(scaled));
            var minimumSignificant = Math.Clamp(options.MinimumSignificantDigits ?? 1, 1, 21);
            var maximumSignificant = Math.Clamp(options.MaximumSignificantDigits ?? 21, minimumSignificant, 21);
            min = Math.Clamp(minimumSignificant - integerDigits, 0, 20);
            max = Math.Clamp(maximumSignificant - integerDigits, min, 20);
            if (integerDigits > maximumSignificant)
            {
                var power = Math.Pow(10, integerDigits - maximumSignificant);
                scaled = Math.Round(scaled / power, MidpointRounding.AwayFromZero) * power;
            }
        }

        var rounded = double.IsFinite(scaled) && max <= 15 ? Math.Round(scaled, max, MidpointRounding.AwayFromZero) : scaled;
        if (notation != NumberValueNotation.Standard && ending.Length > 0 && rounded != scaled)
        {
            var (next, nextEnding) = Scale(magnitude * rounded / scaled, notation, culture);
            if (nextEnding != ending)
            {
                rounded = next;
                ending = nextEnding;
            }
        }

        var minimumInteger = Math.Clamp(options?.MinimumIntegerDigits ?? 1, 1, 21);
        var pattern = (options?.UseGrouping != false && notation != NumberValueNotation.Compact &&
                       notation != NumberValueNotation.Scientific && notation != NumberValueNotation.Engineering ? "#,##" : "") +
                      new string('0', minimumInteger) + (max > 0 ? "." + new string('0', min) + new string('#', max - min) : "");
        string digits = double.IsPositiveInfinity(rounded) ? info.PositiveInfinitySymbol : rounded.ToString(pattern, info);
        digits += ending;
        bool negative = double.IsNegative(input) && sign != NumberValueSignDisplay.Never &&
                        (rounded != 0 || sign is not (NumberValueSignDisplay.ExceptZero or NumberValueSignDisplay.Negative));
        bool positive = !negative && (sign == NumberValueSignDisplay.Always ||
                        sign == NumberValueSignDisplay.ExceptZero && rounded != 0);
        var prefix = negative ? info.NegativeSign : positive ? info.PositiveSign : string.Empty;

        string result = style switch
        {
            NumberValueStyle.Currency => FormatCurrency(digits, currency.Symbol, prefix, negative, options?.Accounting == true, info),
            NumberValueStyle.Percent => FormatPercent(digits, prefix, info),
            NumberValueStyle.Unit => prefix + digits + (options?.UnitLabel ?? FormatUnit(options?.Unit ?? control.Unit, rounded, culture)),
            _ => prefix + digits
        };
        return SubstituteDigits(result, info.NativeDigits);
    }

    private static (double Value, string Suffix) Scale(double value, NumberValueNotation notation, CultureInfo culture)
    {
        if (!double.IsFinite(value) || value <= 0) return (value, "");
        if (notation == NumberValueNotation.Compact)
        {
            var scales = culture.TwoLetterISOLanguageName switch
            {
                "zh" => new[] { (1e12, "万亿"), (1e8, "亿"), (1e4, "万") },
                "ja" => new[] { (1e12, "兆"), (1e8, "億"), (1e4, "万") },
                "fr" => new[] { (1e12, " Bn"), (1e9, " Md"), (1e6, " M"), (1e3, " k") },
                "de" => new[] { (1e12, " Bio."), (1e9, " Mrd."), (1e6, " Mio.") },
                _ => new[] { (1e12, "T"), (1e9, "B"), (1e6, "M"), (1e3, "K") }
            };
            foreach (var (power, suffix) in scales)
                if (value >= power) return (value / power, suffix);
        }
        else if (notation is NumberValueNotation.Scientific or NumberValueNotation.Engineering)
        {
            var exponent = Math.Floor(Math.Log10(value));
            if (notation == NumberValueNotation.Engineering) exponent = Math.Floor(exponent / 3) * 3;
            var power = Math.Pow(10, exponent);
            if (power != 0) return (value / power, "E" + exponent.ToString("0", culture));
            // Extremely small doubles can have an exponent below the range of Math.Pow.
            return (value * Math.Pow(10, 308) / Math.Pow(10, exponent + 308), "E" + exponent.ToString("0", culture));
        }
        return (value, "");
    }

    private static (string Symbol, int Digits) Currency(CultureInfo culture, string? requested, NumberValueCurrencyDisplay display)
    {
        var info = culture.NumberFormat;
        RegionInfo? region = culture.IsNeutralCulture || culture.Name.Length == 0 ? null : new RegionInfo(culture.Name);
        var local = region?.ISOCurrencySymbol;
        var code = requested?.Trim().ToUpperInvariant() ?? local ?? "USD";
        var (symbol, digits) = Currencies.GetValueOrDefault(code, (code, 2));
        if (display == NumberValueCurrencyDisplay.Code) return (code, digits);
        if (display == NumberValueCurrencyDisplay.NarrowSymbol) return (symbol, digits);
        if (code == local) return (info.CurrencySymbol, info.CurrencyDecimalDigits);
        if (code == "CNY" && culture.TwoLetterISOLanguageName != "zh") symbol = "CN¥";
        else if (code == "JPY" && culture.TwoLetterISOLanguageName == "zh") symbol = "JP¥";
        else if (symbol == "$" && code is not ("USD" or "MXN")) symbol = code[..2] + "$";
        return (symbol, digits);
    }

    private static string FormatCurrency(string digits, string symbol, string sign, bool negative, bool accounting, NumberFormatInfo culture)
    {
        var info = (NumberFormatInfo)culture.Clone();
        const string currencyMarker = "\uFFF9";
        info.CurrencySymbol = currencyMarker;
        info.NegativeSign = "\uFFFA";
        string pattern = (negative ? -1 : 1).ToString("C0", info);
        if (negative && !accounting && pattern.StartsWith('('))
            pattern = "\uFFFA" + pattern.Trim('(', ')');
        if (negative && accounting && !pattern.StartsWith('(') && info.CurrencyPositivePattern is 0 or 2)
            pattern = "(" + 1.ToString("C0", info) + ")";
        if (!negative && sign.Length > 0) pattern = "\uFFFA" + pattern;
        if (symbol.Length > 0 && char.IsLetter(symbol[^1])) pattern = pattern.Replace(currencyMarker + NumberMarker, currencyMarker + " " + NumberMarker);
        if (symbol.Length > 0 && char.IsLetter(symbol[0])) pattern = pattern.Replace(NumberMarker + currencyMarker, NumberMarker + " " + currencyMarker);
        return pattern.Replace(NumberMarker, digits).Replace(currencyMarker, symbol).Replace("\uFFFA", sign);
    }

    private static string FormatPercent(string digits, string sign, NumberFormatInfo info) =>
        (info.PercentPositivePattern switch
        {
            0 => sign + digits + " " + info.PercentSymbol,
            2 => sign + info.PercentSymbol + digits,
            3 => sign + info.PercentSymbol + " " + digits,
            _ => sign + digits + info.PercentSymbol
        });

    private static string FormatUnit(string? unit, double value, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(unit)) return "";
        if (culture.TwoLetterISOLanguageName == "zh")
        {
            var translated = unit switch
            {
                "day" => "天", "week" => "周", "month" => "个月", "year" => "年", "hour" => "小时", "minute" => "分钟",
                "second" => "秒", "kilometer" => "公里", "meter" => "米", "kilogram" => "千克", _ => null
            };
            if (translated is not null) return translated;
        }
        if (unit == "day" && value == 1) return " day";
        var per = unit.IndexOf("-per-", StringComparison.Ordinal);
        if (per >= 0) return FormatUnit(unit[..per], value, culture) + "/" + FormatUnit(unit[(per + 5)..], 1, culture).TrimStart();
        return Units.GetValueOrDefault(unit, " " + unit);
    }

    private static string SubstituteDigits(string text, string[] digits) =>
        digits[0] == "0" ? text : string.Concat(text.Select(character => char.IsAsciiDigit(character) ? digits[character - '0'] : character.ToString()));
}
