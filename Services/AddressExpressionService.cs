using System.Globalization;
using System.Text.RegularExpressions;
using MemoryLab.Models;

namespace MemoryLab.Services;

// Handles the address subset shared by saved projects and imported Cheat Engine tables.
internal static class AddressExpressionService
{
    internal sealed record AddressBase(string ModuleName, ulong AbsoluteAddress, long ModuleOffset)
    {
        public bool IsAbsolute => ModuleName.Length == 0;
        public string Expression => IsAbsolute
            ? $"0x{AbsoluteAddress:X}"
            : $"\"{ModuleName}\"{(ModuleOffset < 0 ? string.Empty : "+")}{FormatOffset(ModuleOffset)}";
    }

    public static AddressBase ParseBase(string expression)
    {
        expression = expression.Trim();
        if (TryParseHex(expression, out var absolute))
            return new AddressBase(string.Empty, absolute, 0);

        var match = Regex.Match(expression,
            "^(?:\"(?<module>[^\"|]+)\"|(?<module>[^\\s\"+|]+\\.(?:exe|dll|bin)))(?<offset>\\s*[+-]\\s*(?:0[xX])?[0-9a-fA-F]+)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            throw new FormatException("Alamat harus berupa hex atau module+offset; simbol dan ekspresi skrip belum didukung.");

        var offset = match.Groups["offset"].Success
            ? ParseOffset(match.Groups["offset"].Value.Replace(" ", string.Empty))
            : 0;
        return new AddressBase(match.Groups["module"].Value, 0, offset);
    }

    public static bool TryParseHex(string raw, out ulong value)
    {
        raw = raw.Trim();
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            raw = raw[2..];
        return ulong.TryParse(raw, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    public static long ParseOffset(string raw)
    {
        raw = raw.Trim();
        var negative = raw.StartsWith('-');
        if (raw.StartsWith('+') || negative)
            raw = raw[1..].Trim();
        if (!TryParseHex(raw, out var magnitude) ||
            magnitude > (negative ? 0x8000000000000000UL : long.MaxValue))
            throw new FormatException("Offset hex tidak valid atau di luar rentang 64-bit.");
        return negative ? magnitude == 0x8000000000000000UL ? long.MinValue : -(long)magnitude
            : (long)magnitude;
    }

    public static string FormatOffset(long offset) => offset < 0
        ? $"-0x{(ulong)(-(offset + 1)) + 1:X}"
        : $"0x{offset:X}";

    public static nuint AddOffset(nuint address, long offset) => offset < 0
        ? checked(address - (nuint)((ulong)(-(offset + 1)) + 1))
        : checked(address + (nuint)offset);

    public static nuint Resolve(AddressEntry entry, Func<string, nuint> findModule, Func<nuint, ulong> readPointer)
    {
        if (entry.AddressKind.Equals("Absolute", StringComparison.OrdinalIgnoreCase))
            return entry.Address;

        if (entry.AddressKind.Equals("ModuleOffset", StringComparison.OrdinalIgnoreCase))
        {
            var addressBase = ParseBase(entry.AddressExpression);
            return addressBase.IsAbsolute ? checked((nuint)addressBase.AbsoluteAddress)
                : AddOffset(findModule(addressBase.ModuleName), addressBase.ModuleOffset);
        }

        if (!entry.AddressKind.Equals("PointerChain", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Address kind '{entry.AddressKind}' belum didukung.");

        var parts = entry.AddressExpression.Split('|');
        if (parts.Length != 3)
            throw new FormatException("Pointer expression tidak valid.");

        nuint address;
        if (parts[0].Length == 0)
        {
            if (!TryParseHex(parts[1], out var absolute))
                throw new FormatException("Pointer base tidak valid.");
            address = checked((nuint)absolute);
        }
        else
        {
            address = AddOffset(findModule(parts[0]), ParseOffset(parts[1]));
        }

        if (string.IsNullOrWhiteSpace(parts[2]))
            return address;

        foreach (var rawOffset in parts[2].Split(',', StringSplitOptions.TrimEntries))
        {
            var pointer = readPointer(address);
            if (pointer == 0)
                throw new InvalidOperationException($"Null pointer ditemukan di 0x{address:X16}.");
            address = AddOffset(checked((nuint)pointer), ParseOffset(rawOffset));
        }
        return address;
    }
}
