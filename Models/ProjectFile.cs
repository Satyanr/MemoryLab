namespace MemoryLab.Models;

public sealed class ProjectFile
{
    public int Version { get; set; } = 1;
    public string ProjectName { get; set; } = "MemoryLab Project";
    public string ProcessName { get; set; } = string.Empty;
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ProjectAddressEntry> Addresses { get; set; } = new();
    public List<ProjectPointerEntry> Pointers { get; set; } = new();
}

public sealed class ProjectAddressEntry
{
    public string Description { get; set; } = string.Empty;
    public string Group { get; set; } = "Default";
    public string ValueType { get; set; } = "Int32";
    public string AddressKind { get; set; } = "Absolute";
    public string AddressExpression { get; set; } = string.Empty;
    public ulong AbsoluteAddress { get; set; }
    public bool Frozen { get; set; }
    public string FrozenValue { get; set; } = string.Empty;
}

public sealed class ProjectPointerEntry
{
    public string Description { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public ulong BaseOffset { get; set; }
    public List<ulong> Offsets { get; set; } = new();
    public string ValueType { get; set; } = "Int32";
}
