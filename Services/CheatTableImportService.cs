using System.IO;
using System.Xml;
using System.Xml.Linq;
using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class CheatTableImportService
{
    public async Task<CheatTableImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 64 * 1024 * 1024
        });
        try
        {
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            return Parse(document, Path.GetFileNameWithoutExtension(filePath), cancellationToken);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("File bukan tabel Cheat Engine XML (.CT) yang valid. Tabel terkompresi belum didukung.", ex);
        }
    }

    internal static CheatTableImportResult Parse(XDocument document, string tableName, CancellationToken cancellationToken = default)
    {
        var root = document.Root;
        if (root?.Name != "CheatTable" || root.Element("CheatEntries") is not { } entries)
            throw new InvalidDataException("File .CT harus berisi CheatTable dan CheatEntries.");

        var result = new CheatTableImportResult
        {
            TableName = tableName,
            HasTableLua = !string.IsNullOrWhiteSpace(root.Element("LuaScript")?.Value)
        };
        var processModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Iterative traversal preserves table order without a recursion limit on nested groups.
        var pending = new Stack<(XElement Entry, string Group)>();
        PushChildren(entries, string.Empty);
        while (pending.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = item.Entry;
            var description = Unquote(entry.Element("Description")?.Value ?? string.Empty);
            if (string.IsNullOrWhiteSpace(description))
                description = $"Entry {entry.Element("ID")?.Value ?? "(unnamed)"}";

            if (entry.Element("GroupHeader")?.Value.Trim() != "1")
            {
                try
                {
                    var valueType = MapValueType(entry);
                    var addressBase = AddressExpressionService.ParseBase(entry.Element("Address")?.Value ?? string.Empty);
                    var offsets = entry.Element("Offsets")?.Elements("Offset")
                        .Select(offset => AddressExpressionService.ParseOffset(offset.Value)).Reverse().ToArray()
                        ?? Array.Empty<long>();
                    var kind = offsets.Length > 0 ? "PointerChain" : addressBase.IsAbsolute ? "Absolute" : "ModuleOffset";
                    var expression = offsets.Length > 0
                        ? $"{addressBase.ModuleName}|{(addressBase.IsAbsolute ? $"0x{addressBase.AbsoluteAddress:X}" : AddressExpressionService.FormatOffset(addressBase.ModuleOffset))}|{string.Join(",", offsets.Select(AddressExpressionService.FormatOffset))}"
                        : addressBase.Expression;

                    result.Entries.Add(new AddressEntry
                    {
                        Description = description,
                        Group = item.Group.Length == 0 ? "Default" : item.Group,
                        ValueType = valueType,
                        AddressKind = kind,
                        AddressExpression = expression,
                        Address = kind == "Absolute" ? checked((nuint)addressBase.AbsoluteAddress) : 0,
                        Value = "<not attached>",
                        IsFrozen = false
                    });
                    if (addressBase.ModuleName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        processModules.Add(addressBase.ModuleName[..^4]);
                }
                catch (Exception ex) when (ex is FormatException or NotSupportedException or OverflowException)
                {
                    var path = item.Group.Length == 0 ? description : $"{item.Group} / {description}";
                    result.SkippedEntries.Add($"{path}: {ex.Message}");
                }
            }

            if (entry.Element("CheatEntries") is { } children)
                PushChildren(children, item.Group.Length == 0 ? description : $"{item.Group} / {description}");
        }

        if (processModules.Count == 1)
            result.ProcessName = processModules.Single();
        return result;

        void PushChildren(XElement parent, string group)
        {
            foreach (var child in parent.Elements("CheatEntry").Reverse())
                pending.Push((child, group));
        }
    }

    private static string MapValueType(XElement entry)
    {
        var signed = entry.Element("ShowAsSigned")?.Value.Trim() == "1";
        var type = entry.Element("VariableType")?.Value.Trim() ?? string.Empty;
        if (entry.Element("AssemblerScript") is not null)
            throw new NotSupportedException("Auto Assembler Script belum didukung.");
        return type.ToUpperInvariant() switch
        {
            "BYTE" => signed ? "SByte" : "Byte",
            "2 BYTES" => signed ? "Int16" : "UInt16",
            "4 BYTES" => signed ? "Int32" : "UInt32",
            "8 BYTES" => signed ? "Int64" : "UInt64",
            "FLOAT" => "Float",
            "DOUBLE" => "Double",
            _ => throw new NotSupportedException($"Tipe '{type}' belum didukung.")
        };
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }
}
