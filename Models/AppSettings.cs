namespace MemoryLab.Models;

public sealed class AppSettings
{
    public int AddressRefreshMs { get; set; } = 250;
    public int MemoryViewerRefreshMs { get; set; } = 500;
    public int ScanResultLimit { get; set; } = 1_000_000;
}
