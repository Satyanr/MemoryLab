using System.Text.Json;
using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(
        string filePath,
        ProjectFile project,
        CancellationToken cancellationToken = default)
    {
        project.SavedAtUtc = DateTime.UtcNow;

        await using var stream = File.Create(filePath);

        await JsonSerializer.SerializeAsync(
            stream,
            project,
            JsonOptions,
            cancellationToken);
    }

    public async Task<ProjectFile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);

        var project = await JsonSerializer.DeserializeAsync<ProjectFile>(
            stream,
            JsonOptions,
            cancellationToken);

        return project
            ?? throw new InvalidDataException("Project file kosong atau tidak valid.");
    }
}
