using System.Globalization;
using System.Windows.Media;

namespace ChemistryReactionEditor;

static class ColorConversion
{
    public static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        string value = hex.Trim();
        if (!value.StartsWith('#'))
            value = "#" + value;

        if (value.Length != 7 && value.Length != 9)
            return false;

        if (!byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r))
            return false;
        if (!byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g))
            return false;
        if (!byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            return false;

        byte a = 255;
        if (value.Length == 9 &&
            !byte.TryParse(value.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a))
            return false;

        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    public static string ToHex(Color color, bool includeAlpha)
    {
        return includeAlpha
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static (double h, double s, double v) ToHsv(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h;
        if (delta < 1e-6)
            h = 0;
        else if (max == r)
            h = 60 * (((g - b) / delta) % 6);
        else if (max == g)
            h = 60 * (((b - r) / delta) + 2);
        else
            h = 60 * (((r - g) / delta) + 4);

        if (h < 0)
            h += 360;

        double s = max < 1e-6 ? 0 : delta / max;
        double v = max;
        return (h, s, v);
    }

    public static Color FromHsv(double h, double s, double v, byte alpha = 255)
    {
        h = (h % 360 + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;

        double rPrime;
        double gPrime;
        double bPrime;
        if (h < 60)
        {
            rPrime = c;
            gPrime = x;
            bPrime = 0;
        }
        else if (h < 120)
        {
            rPrime = x;
            gPrime = c;
            bPrime = 0;
        }
        else if (h < 180)
        {
            rPrime = 0;
            gPrime = c;
            bPrime = x;
        }
        else if (h < 240)
        {
            rPrime = 0;
            gPrime = x;
            bPrime = c;
        }
        else if (h < 300)
        {
            rPrime = x;
            gPrime = 0;
            bPrime = c;
        }
        else
        {
            rPrime = c;
            gPrime = 0;
            bPrime = x;
        }

        return Color.FromArgb(
            alpha,
            (byte)Math.Round((rPrime + m) * 255),
            (byte)Math.Round((gPrime + m) * 255),
            (byte)Math.Round((bPrime + m) * 255));
    }

    public static Color NormalizeOrDefault(string? hex)
    {
        return TryParseHex(hex, out Color color) ? color : Colors.White;
    }
}
