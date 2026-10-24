namespace MemoryLab.Models;

public sealed class CheatTableImportResult
{
    public string TableName { get; init; } = string.Empty;
    public string ProcessName { get; internal set; } = string.Empty;
    public List<AddressEntry> Entries { get; } = new();
    public List<string> SkippedEntries { get; } = new();
    public bool HasTableLua { get; internal set; }

    public string Summary => $"Imported {Entries.Count:N0} address(es); skipped {SkippedEntries.Count:N0} entry/entries.";
}
