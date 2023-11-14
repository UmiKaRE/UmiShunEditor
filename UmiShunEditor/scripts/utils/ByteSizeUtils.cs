using System;

// Shamelessly stolen from https://stackoverflow.com/a/22733709
public static class ByteSizeUtils
{
    public enum SizeUnits
    {
        Byte, KB, MB, GB, TB, PB, EB, ZB, YB
    }

    public static string ToSizeKibi(this long value, SizeUnits unit)
    {
        return (value / (double)Math.Pow(1024, (long)unit)).ToString("0.00");
    }

	public static string ToSizeKilo(this long value, SizeUnits unit)
    {
        return (value / (double)Math.Pow(1000, (long)unit)).ToString("0.00");
    }
}