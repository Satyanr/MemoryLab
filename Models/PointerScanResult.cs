namespace MemoryLab.Models;

public sealed class PointerScanResult
{
    public nuint PointerAddress { get; init; }
    public nuint PointerValue { get; init; }
    public nuint TargetAddress { get; init; }
    public nuint Offset { get; init; }
    public string PointerLocation { get; init; } = string.Empty;

    public string PointerAddressText => $"0x{PointerAddress:X16}";
    public string PointerValueText => $"0x{PointerValue:X16}";
    public string OffsetText => $"+0x{Offset:X}";
}
