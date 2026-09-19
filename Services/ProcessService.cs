using System.Diagnostics;
using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class ProcessService
{
    public IReadOnlyList<ProcessItem> GetProcesses()
    {
        var items = new List<ProcessItem>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                var title = process.MainWindowTitle ?? string.Empty;

                items.Add(new ProcessItem
                {
                    Id = process.Id,
                    Name = name,
                    Title = title
                });
            }
            catch
            {
                // Beberapa system process menolak akses metadata.
            }
            finally
            {
                process.Dispose();
            }
        }

        return items
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)
            .ToList();
    }

    public Process? TryGetProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch
        {
            return null;
        }
    }
}
