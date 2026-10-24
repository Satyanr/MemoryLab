using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using MemoryLab.Models;
using MemoryLab.Services;

internal static class CheatTableImportChecks
{
    public static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "game.CT");

    public static void Run()
    {
        var service = new CheatTableImportService();
        var result = service.ImportAsync(FixturePath).GetAwaiter().GetResult();
        Assert(result.Entries.Count == 6 && result.SkippedEntries.Count == 4, "partial import counts");
        Assert(result.TableName == "game" && result.ProcessName == "game", "table name and unambiguous EXE target");
        Assert(result.HasTableLua, "table Lua is reported");
        var health = result.Entries[0];
        Assert(health.Description == "Health" && health.Group == "Player & stats", "description, XML entities and group");
        Assert(health.ValueType == "Int32" && health.AddressExpression == "game.exe|0x1234|-0x20,0x10", "pointer order and signed offset");
        Assert(health.Address == 0 && !health.IsFrozen && health.FrozenValue.Length == 0, "LastState is never a resolved address or activation");
        Assert(result.Entries[1].Group == "Player & stats / Movement", "nested groups");
        Assert(result.Entries[2].Address == 0x7FF000001234 && result.Entries[2].ValueType == "UInt64", "64-bit absolute address and unsigned type");
        Assert(result.Entries[3].AddressExpression == "|0x1000|0x8", "absolute pointer base");
        Assert(result.Entries[4].Group == "Enable script", "independent child of skipped script is retained");
        Assert(result.Entries[5].ValueType == "UInt16", "unsigned word type");
        Assert(result.SkippedEntries.Any(item => item.Contains("Enable script / Symbol health")), "symbol skip identifies its parent");

        var reads = new List<nuint>();
        var address = AddressExpressionService.Resolve(health, name => name == "game.exe" ? 0x100000u : throw new Exception(name), location =>
        {
            reads.Add(location);
            return location switch { 0x101234 => 0x2000, 0x1FE0 => 0x3000, _ => throw new Exception($"Unexpected pointer read {location:X}") };
        });
        Assert(address == 0x3010 && reads.SequenceEqual(new nuint[] { 0x101234, 0x1FE0 }), "CE pointer chain dereferences from last offset to first");
        Assert(AddressExpressionService.Resolve(result.Entries[1], _ => 0x100000, _ => throw new Exception()) == 0xFFFE0, "negative module offset");
        Assert(AddressExpressionService.Resolve(result.Entries[3], _ => throw new Exception(), _ => 0x5000) == 0x5008, "absolute pointer resolves");
        Assert(AddressExpressionService.Resolve(new AddressEntry { AddressKind = "ModuleOffset", AddressExpression = "game.exe+0x1234" }, _ => 0x100000, _ => 0) == 0x101234, "existing module project format");
        Assert(AddressExpressionService.Resolve(new AddressEntry { AddressKind = "PointerChain", AddressExpression = "game.exe|0x100|0x10,0x20" }, _ => 0x1000, a => a + 0x1000) == 0x3130, "existing pointer project format");
        Assert(AddressExpressionService.ParseOffset("-8000000000000000") == long.MinValue, "signed offset lower boundary");
        AssertThrows<OverflowException>(() => AddressExpressionService.AddOffset(0, -1), "address underflow rejected");
        AssertThrows<InvalidOperationException>(() => AddressExpressionService.Resolve(health, _ => 1, _ => 0), "null pointer rejected");

        foreach (var (ceType, unsigned, signed) in new[] {
                     ("Byte", "Byte", "SByte"), ("2 Bytes", "UInt16", "Int16"),
                     ("4 Bytes", "UInt32", "Int32"), ("8 Bytes", "UInt64", "Int64"),
                     ("Float", "Float", "Float"), ("Double", "Double", "Double") })
        {
            foreach (var signedFlag in new[] { false, true })
            {
                var document = XDocument.Parse($"<CheatTable><CheatEntries><CheatEntry><VariableType>{ceType}</VariableType><ShowAsSigned>{(signedFlag ? 1 : 0)}</ShowAsSigned><Address>1234</Address></CheatEntry></CheatEntries></CheatTable>");
                Assert(CheatTableImportService.Parse(document, "types").Entries.Single().ValueType == (signedFlag ? signed : unsigned), $"{ceType} signed={signedFlag}");
            }
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"memorylab-import-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, "<NotATable/>");
            AssertThrows<InvalidDataException>(() => service.ImportAsync(tempPath).GetAwaiter().GetResult(), "wrong XML root rejected");
            File.WriteAllText(tempPath, "<CheatTable><CheatEntries>");
            AssertThrows<InvalidDataException>(() => service.ImportAsync(tempPath).GetAwaiter().GetResult(), "malformed XML rejected");
            File.WriteAllText(tempPath, "<!DOCTYPE CheatTable [<!ENTITY value SYSTEM 'file:///should-not-be-read'>]><CheatTable><CheatEntries>&value;</CheatEntries></CheatTable>");
            AssertThrows<InvalidDataException>(() => service.ImportAsync(tempPath).GetAwaiter().GetResult(), "external entities rejected");
            AssertThrows<OperationCanceledException>(() => service.ImportAsync(FixturePath, new CancellationToken(true)).GetAwaiter().GetResult(), "cancelled import");

            var project = new ProjectFile { Addresses = result.Entries.Select(entry => new ProjectAddressEntry
            {
                Description = entry.Description, Group = entry.Group, ValueType = entry.ValueType,
                AddressKind = entry.AddressKind, AddressExpression = entry.AddressExpression, AbsoluteAddress = (ulong)entry.Address
            }).ToList() };
            var projects = new ProjectService();
            projects.SaveAsync(tempPath, project).GetAwaiter().GetResult();
            var reloaded = projects.LoadAsync(tempPath).GetAwaiter().GetResult();
            Assert(reloaded.Addresses.Count == 6 && reloaded.Addresses[0].AddressExpression == health.AddressExpression &&
                reloaded.Addresses[3].AddressExpression == "|0x1000|0x8", "imported pointers persist through project save/load");
        }
        finally { File.Delete(tempPath); }

        CheckNumericMemory();
        Console.WriteLine("PASS: CT import, groups, partial failures, pointer order, persistence, and numeric memory access.");
    }

    private static void CheckNumericMemory()
    {
        using var memory = new MemoryService();
        memory.Attach(Environment.ProcessId);
        Assert(memory.PointerSize == 8, "64-bit target pointer size");
        var allocation = Marshal.AllocHGlobal(64);
        var address = unchecked((nuint)allocation.ToInt64());
        try
        {
            foreach (var (type, value) in new[] {
                         ("Byte", "255"), ("SByte", "-128"), ("Int16", "-32768"), ("UInt16", "65535"),
                         ("Int32", "-2147483648"), ("UInt32", "4294967295"), ("Int64", "-9223372036854775808"),
                         ("UInt64", "18446744073709551615"), ("Float", "12.5"), ("Double", "-12345.125") })
            {
                memory.WriteBytes(address, Enumerable.Repeat((byte)0xA5, 16).ToArray());
                memory.WriteValueFromString(address, type, value);
                Assert(memory.ReadValueAsString(address, type) == value, $"{type} live read/write round trip");
                Assert(memory.ReadBytes(address + (nuint)MemoryValueCodec.GetSize(type), 1)[0] == 0xA5, $"{type} writes correct byte count");
            }
            AssertThrows<FormatException>(() => memory.WriteValueFromString(address, "Byte", "256"), "numeric overflow rejected");
            AssertThrows<FormatException>(() => memory.WriteValueFromString(address, "UInt64", "-1"), "unsigned negative rejected");
            AssertThrows<FormatException>(() => memory.WriteValueFromString(address, "Double", "oops"), "invalid number rejected");
            memory.WriteBytes(address, BitConverter.GetBytes((ulong)address + 16));
            memory.WriteBytes(address + 16, BitConverter.GetBytes((ulong)address + 32));
            var pointer = new AddressEntry { AddressKind = "PointerChain", AddressExpression = $"|0x{address:X}|0x0,0x4" };
            Assert(AddressExpressionService.Resolve(pointer, _ => throw new Exception(), memory.ReadPointer) == address + 36, "live pointer resolution");
            memory.WriteBytes(address + 16, BitConverter.GetBytes((ulong)address + 48));
            Assert(AddressExpressionService.Resolve(pointer, _ => throw new Exception(), memory.ReadPointer) == address + 52, "live pointer follows moved object");
        }
        finally { Marshal.FreeHGlobal(allocation); }
    }

    private static void AssertThrows<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"FAIL: {message}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
    }
}
