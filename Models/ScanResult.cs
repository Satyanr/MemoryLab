namespace MemoryLab.Models;

public sealed class ScanResult
{
    public nuint Address { get; init; }

    public string AddressText => $"0x{Address:X16}";

    public string Value { get; set; } = string.Empty;

    public string PreviousValue { get; set; } = string.Empty;

    // Numeric snapshots used by advanced Next Scan filters.
    public double NumericValue { get; set; }

    public double PreviousNumericValue { get; set; }

    public string ValueType { get; set; } = "Int32";
}
