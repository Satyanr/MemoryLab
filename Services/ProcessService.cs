using System.Diagnostics;
using MemoryLab.Native;
using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class ProcessService
{
    public IReadOnlyList<ProcessItem> GetProcesses()
    {
        var items = new List<ProcessItem>();
        using var currentProcess = Process.GetCurrentProcess();
        var sessionId = currentProcess.SessionId;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                if (process.Id == Environment.ProcessId ||
                    process.SessionId != sessionId ||
                    GameProcessFilter.IsExcluded(name))
                    continue;

                var window = process.MainWindowHandle;
                if (window == IntPtr.Zero || !NativeMethods.IsWindowVisible(window))
                    continue;

                var title = process.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                // Protected processes can deny module access. Keep checking other apps.
                string? executablePath = null;
                try { executablePath = process.MainModule?.FileName; }
                catch { }

                if (GameProcessFilter.IsExcluded(name, executablePath))
                    continue;

                var isGame = GameProcessFilter.IsGame(name, title, executablePath, true);
                if (!isGame)
                {
                    try
                    {
                        isGame = GameProcessFilter.IsGame(name, title, executablePath, true,
                            process.Modules.Cast<ProcessModule>().Select(module => module.ModuleName));
                    }
                    catch { }
                }

                if (!isGame)
                    continue;

                items.Add(new ProcessItem
                {
                    Id = process.Id,
                    Name = name,
                    Title = title
                });
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return items
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
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


    public ProcessItem? FindProcessByName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return null;

        var normalized = processName.EndsWith(
            ".exe",
            StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        if (GameProcessFilter.IsExcluded(normalized))
            return null;

        // Explicit project targets also support owned test apps without a game window.
        var processes = Process.GetProcessesByName(normalized);
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            foreach (var process in processes.OrderBy(p => p.Id))
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.SessionId != currentProcess.SessionId)
                        continue;

                    if (GameProcessFilter.IsExcluded(normalized, process.MainModule?.FileName))
                        continue;

                    return new ProcessItem
                    {
                        Id = process.Id,
                        Name = process.ProcessName,
                        Title = process.MainWindowTitle
                    };
                }
                catch { }
            }

            return null;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    public IReadOnlyList<ModuleInfo> GetModules(int processId)
    {
        using var process = Process.GetProcessById(processId);
        var modules = new List<ModuleInfo>();

        foreach (ProcessModule module in process.Modules)
        {
            try
            {
                modules.Add(new ModuleInfo
                {
                    Name = module.ModuleName,
                    BaseAddress = unchecked((nuint)module.BaseAddress.ToInt64()),
                    ModuleSize = checked((nuint)module.ModuleMemorySize)
                });
            }
            catch
            {
            }
        }

        return modules
            .OrderBy(m => m.BaseAddress)
            .ToList();
    }


    public ModuleInfo? FindModuleByName(
        int processId,
        string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            return null;

        return GetModules(processId)
            .FirstOrDefault(m =>
                string.Equals(
                    m.Name,
                    moduleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    public ModuleInfo? FindContainingModule(int processId, nuint address)
    {
        return GetModules(processId)
            .FirstOrDefault(m =>
                address >= m.BaseAddress &&
                address < m.EndAddress);
    }

    public string FormatModuleRelativeAddress(int processId, nuint address)
    {
        try
        {
            var module = FindContainingModule(processId, address);

            if (module is null)
                return $"0x{address:X16}";

            var offset = address - module.BaseAddress;
            return $"{module.Name}+0x{offset:X}";
        }
        catch
        {
            return $"0x{address:X16}";
        }
    }
}
