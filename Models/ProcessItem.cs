namespace MemoryLab.Models;

public sealed class ProcessItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Title)
            ? $"{Name}.exe  (PID {Id})"
            : $"{Name}.exe  (PID {Id}) — {Title}";
}
