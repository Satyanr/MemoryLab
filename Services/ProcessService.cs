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


    public ProcessItem? FindProcessByName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return null;

        var normalized = processName.EndsWith(
            ".exe",
            StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        return GetProcesses()
            .FirstOrDefault(p =>
                string.Equals(
                    p.Name,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));
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
