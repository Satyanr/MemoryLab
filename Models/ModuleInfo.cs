namespace MemoryLab.Models;

public sealed class ModuleInfo
{
    public string Name { get; init; } = string.Empty;
    public nuint BaseAddress { get; init; }
    public nuint ModuleSize { get; init; }

    public nuint EndAddress => BaseAddress + ModuleSize;

    public string BaseAddressText => $"0x{BaseAddress:X16}";

    public string DisplayName =>
        $"{Name} — {BaseAddressText} — {ModuleSize:N0} bytes";
}
