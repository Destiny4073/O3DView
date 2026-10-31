using System;
using System.Globalization;

namespace O3DParse;

/// <summary>
/// Lenient, culture-invariant number parsing with Delphi semantics (StrToFloat/StrToInt with
/// a default of zero): garbage reads as zero, trailing text after the first whitespace
/// separated token is ignored ("5 (metres)" reads as 5), a comma is accepted as the decimal
/// separator, and values that are not finite are garbage like any other. Ported from
/// openOMSI's omsi-cfg (number.rs), which reverse engineered Omsi.exe.
/// </summary>
public static class OmsiNumbers
{
    /// <summary>
    /// Parses a double the way OMSI does: decimal point, optional exponent, garbage → 0.
    /// </summary>
    public static double ParseDouble(ReadOnlySpan<char> s)
    {
        s = s.Trim();
        if (s.IsEmpty)
            return 0.0;

        // Take the first whitespace-separated token, so a trailing comment or unit is ignored
        int end = 0;
        while (end < s.Length && !char.IsWhiteSpace(s[end]))
            end++;
        ReadOnlySpan<char> token = s[..end];

        // Delphi accepts ',' as the decimal separator, and "1." / ".5"
        string t = token.ToString().Replace(',', '.');
        if (t.EndsWith('.'))
            t += '0';
        if (t.StartsWith('.') || t.StartsWith("-."))
            t = t.Replace(".", "0.");

        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return double.IsFinite(v) ? v : 0.0;

        // Fall back to the longest numeric prefix
        int numEnd = 0;
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (char.IsAsciiDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E')
                numEnd = i + 1;
            else
                break;
        }
        if (numEnd > 0
            && double.TryParse(t.AsSpan(0, numEnd), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
            && double.IsFinite(v))
            return v;
        return 0.0;
    }

    /// <summary>Parses a float; a double beyond the float's range reads as zero.</summary>
    public static float ParseFloat(ReadOnlySpan<char> s)
    {
        float v = (float)ParseDouble(s);
        return float.IsFinite(v) ? v : 0f;
    }

    /// <summary>
    /// Parses a 64-bit integer the way OMSI does: a plain integer is read directly, anything
    /// else falls back to a truncated <see cref="ParseDouble"/> ("3.0" reads as 3).
    /// </summary>
    public static long ParseLong(ReadOnlySpan<char> s)
    {
        s = s.Trim();
        if (s.IsEmpty)
            return 0;

        int end = 0;
        while (end < s.Length && !char.IsWhiteSpace(s[end]))
            end++;
        ReadOnlySpan<char> token = s[..end];

        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v))
            return v;
        // Delphi StrToInt fails on "1.0"; OMSI usually uses StrToFloat and truncates
        return DoubleToLong(token);
    }

    /// <summary>Parses an int, clamped to its range like the original.</summary>
    public static int ParseInt32(ReadOnlySpan<char> s)
    {
        long v = ParseLong(s);
        return v > int.MaxValue ? int.MaxValue : v < int.MinValue ? int.MinValue : (int)v;
    }

    /// <summary>Parses an unsigned int; a negative value reads as zero.</summary>
    public static uint ParseUInt32(ReadOnlySpan<char> s)
    {
        long v = ParseLong(s);
        return v <= 0 ? 0u : v >= uint.MaxValue ? uint.MaxValue : (uint)v;
    }

    /// <summary>Parses a byte, clamped to its range.</summary>
    public static byte ParseByte(ReadOnlySpan<char> s)
    {
        long v = ParseLong(s);
        return v > byte.MaxValue ? byte.MaxValue : v < 0 ? (byte)0 : (byte)v;
    }

    /// <summary>Parses an sbyte, clamped to its range.</summary>
    public static sbyte ParseSByte(ReadOnlySpan<char> s)
    {
        long v = ParseLong(s);
        return v > sbyte.MaxValue ? sbyte.MaxValue : v < sbyte.MinValue ? sbyte.MinValue : (sbyte)v;
    }

    /// <summary>
    /// Parses a boolean: "1" or "true" (case-insensitive) is true, everything else false.
    /// As a tolerance for drive shares and the like, any other text that reads as a
    /// non-zero number is also true (Omsi reads [achse_antrieb] that way).
    /// </summary>
    public static bool ParseBool(ReadOnlySpan<char> s)
    {
        s = s.Trim();
        if (s.SequenceEqual("1"))
            return true;
        if (s.IsEmpty || s.SequenceEqual("0"))
            return false;
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;
        return ParseFloat(s) != 0f;
    }

    /// <summary>Float to long with saturating conversion, as Rust's `as` and the exe do
    /// (NaN reads as zero).</summary>
    private static long DoubleToLong(ReadOnlySpan<char> s)
    {
        double d = ParseDouble(s);
        if (double.IsNaN(d))
            return 0;
        if (d >= long.MaxValue)
            return long.MaxValue;
        if (d <= long.MinValue)
            return long.MinValue;
        return (long)d;
    }
}
