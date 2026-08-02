namespace VseInstrumentiParser;

public class DimensionsData
{
    public decimal WeightKg { get; set; }
    public decimal LengthMm { get; set; }
    public decimal WidthMm { get; set; }
    public decimal HeightMm { get; set; }

    internal static DimensionsData Parse(string? weight, string? length, string? width, string? height)
    {
        return new DimensionsData
        {
            WeightKg = decimal.Parse(weight?.Replace(".", ",") ?? "0"),
            LengthMm = decimal.Parse(length?.Replace(".", ",") ?? "0"),
            WidthMm = decimal.Parse(width?.Replace(".", ",") ?? "0"),
            HeightMm = decimal.Parse(height?.Replace(".", ",") ?? "0")
        };
    }
}