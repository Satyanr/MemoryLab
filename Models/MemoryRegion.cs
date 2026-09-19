namespace MemoryLab.Models;

public sealed class MemoryRegion
{
    public nuint BaseAddress { get; init; }
    public nuint RegionSize { get; init; }
    public uint State { get; init; }
    public uint Protect { get; init; }
    public uint Type { get; init; }

    public nuint EndAddress => BaseAddress + RegionSize;
}
