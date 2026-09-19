using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using MemoryLab.Models;
using MemoryLab.Native;

namespace MemoryLab.Services;

public sealed class MemoryService : IDisposable
{
    private IntPtr _processHandle = IntPtr.Zero;

    public int? AttachedProcessId { get; private set; }
    public bool IsAttached => _processHandle != IntPtr.Zero;

    public void Attach(int processId)
    {
        Detach();

        const NativeMethods.ProcessAccessFlags access =
            NativeMethods.ProcessAccessFlags.QueryInformation |
            NativeMethods.ProcessAccessFlags.VirtualMemoryRead |
            NativeMethods.ProcessAccessFlags.VirtualMemoryWrite |
            NativeMethods.ProcessAccessFlags.VirtualMemoryOperation;

        _processHandle = NativeMethods.OpenProcess(access, false, processId);

        if (_processHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "Gagal membuka process.");
        }

        AttachedProcessId = processId;
    }

    public IReadOnlyList<MemoryRegion> GetReadableRegions()
    {
        EnsureAttached();

        var regions = new List<MemoryRegion>();
        nuint address = 0;
        var mbiSize = (nuint)Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION>();

        while (true)
        {
            var result = NativeMethods.VirtualQueryEx(
                _processHandle,
                new IntPtr(unchecked((long)address)),
                out var mbi,
                mbiSize);

            if (result == 0)
                break;

            var baseAddress = unchecked((nuint)mbi.BaseAddress.ToInt64());
            var size = mbi.RegionSize;

            if (size == 0)
                break;

            if (IsReadableCommitted(mbi))
            {
                regions.Add(new MemoryRegion
                {
                    BaseAddress = baseAddress,
                    RegionSize = size,
                    State = mbi.State,
                    Protect = mbi.Protect,
                    Type = mbi.Type
                });
            }

            var next = baseAddress + size;

            if (next <= address)
                break;

            address = next;
        }

        return regions;
    }

    public byte[] ReadBytes(nuint address, int count)
    {
        EnsureAttached();

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var buffer = new byte[count];

        var ok = NativeMethods.ReadProcessMemory(
            _processHandle,
            new IntPtr(unchecked((long)address)),
            buffer,
            (nuint)count,
            out var bytesRead);

        if (!ok)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "ReadProcessMemory gagal.");
        }

        if (bytesRead != (nuint)count)
            Array.Resize(ref buffer, checked((int)bytesRead));

        return buffer;
    }

    public bool TryReadBytes(nuint address, int count, out byte[] buffer)
    {
        buffer = new byte[count];

        if (!IsAttached || count <= 0)
            return false;

        var ok = NativeMethods.ReadProcessMemory(
            _processHandle,
            new IntPtr(unchecked((long)address)),
            buffer,
            (nuint)count,
            out var bytesRead);

        if (!ok || bytesRead == 0)
        {
            buffer = Array.Empty<byte>();
            return false;
        }

        if (bytesRead != (nuint)count)
            Array.Resize(ref buffer, checked((int)bytesRead));

        return true;
    }

    public void WriteBytes(nuint address, byte[] bytes)
    {
        EnsureAttached();

        if (bytes.Length == 0)
            throw new ArgumentException("Data kosong.", nameof(bytes));

        var ok = NativeMethods.WriteProcessMemory(
            _processHandle,
            new IntPtr(unchecked((long)address)),
            bytes,
            (nuint)bytes.Length,
            out var bytesWritten);

        if (!ok)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "WriteProcessMemory gagal.");
        }

        if (bytesWritten != (nuint)bytes.Length)
            throw new InvalidOperationException("Tidak semua byte berhasil ditulis.");
    }


    public ulong ReadUInt64(nuint address)
    {
        var bytes = ReadBytes(address, sizeof(ulong));

        if (bytes.Length != sizeof(ulong))
            throw new InvalidOperationException("Jumlah byte pointer yang terbaca tidak lengkap.");

        return BitConverter.ToUInt64(bytes, 0);
    }

    public bool TryReadUInt64(nuint address, out ulong value)
    {
        value = 0;

        if (!TryReadBytes(address, sizeof(ulong), out var bytes) ||
            bytes.Length != sizeof(ulong))
        {
            return false;
        }

        value = BitConverter.ToUInt64(bytes, 0);
        return true;
    }

    public int ReadInt32(nuint address)
    {
        var bytes = ReadBytes(address, sizeof(int));

        if (bytes.Length != sizeof(int))
            throw new InvalidOperationException("Jumlah byte yang terbaca tidak lengkap.");

        return BitConverter.ToInt32(bytes, 0);
    }

    public float ReadFloat(nuint address)
    {
        var bytes = ReadBytes(address, sizeof(float));

        if (bytes.Length != sizeof(float))
            throw new InvalidOperationException("Jumlah byte yang terbaca tidak lengkap.");

        return BitConverter.ToSingle(bytes, 0);
    }

    public void WriteInt32(nuint address, int value)
    {
        WriteBytes(address, BitConverter.GetBytes(value));
    }

    public void WriteFloat(nuint address, float value)
    {
        WriteBytes(address, BitConverter.GetBytes(value));
    }

    public string ReadValueAsString(nuint address, string valueType)
    {
        return valueType switch
        {
            "Int32" => ReadInt32(address).ToString(CultureInfo.InvariantCulture),
            "Float" => ReadFloat(address).ToString("G9", CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"Value type '{valueType}' belum didukung.")
        };
    }

    public void WriteValueFromString(nuint address, string valueType, string rawValue)
    {
        rawValue = rawValue.Trim();

        switch (valueType)
        {
            case "Int32":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    throw new FormatException("Nilai Int32 tidak valid.");

                WriteInt32(address, intValue);
                break;

            case "Float":
                if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    throw new FormatException("Nilai Float tidak valid. Gunakan format seperti 12.5.");

                WriteFloat(address, floatValue);
                break;

            default:
                throw new NotSupportedException($"Value type '{valueType}' belum didukung.");
        }
    }


    public MemoryRegion? GetRegionForAddress(nuint address)
    {
        EnsureAttached();

        var mbiSize = (nuint)Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION>();

        var result = NativeMethods.VirtualQueryEx(
            _processHandle,
            new IntPtr(unchecked((long)address)),
            out var mbi,
            mbiSize);

        if (result == 0)
            return null;

        return new MemoryRegion
        {
            BaseAddress = unchecked((nuint)mbi.BaseAddress.ToInt64()),
            RegionSize = mbi.RegionSize,
            State = mbi.State,
            Protect = mbi.Protect,
            Type = mbi.Type
        };
    }

    public string GetProtectionText(uint protect)
    {
        if ((protect & NativeMethods.PAGE_GUARD) != 0)
            return "GUARD";

        var core = protect & 0xFF;

        return core switch
        {
            NativeMethods.PAGE_NOACCESS => "NOACCESS",
            NativeMethods.PAGE_READONLY => "READONLY",
            NativeMethods.PAGE_READWRITE => "READWRITE",
            NativeMethods.PAGE_WRITECOPY => "WRITECOPY",
            NativeMethods.PAGE_EXECUTE => "EXECUTE",
            NativeMethods.PAGE_EXECUTE_READ => "EXECUTE_READ",
            NativeMethods.PAGE_EXECUTE_READWRITE => "EXECUTE_READWRITE",
            NativeMethods.PAGE_EXECUTE_WRITECOPY => "EXECUTE_WRITECOPY",
            _ => $"0x{protect:X}"
        };
    }

    public string GetStateText(uint state)
    {
        return state switch
        {
            0x1000 => "MEM_COMMIT",
            0x10000 => "MEM_FREE",
            0x2000 => "MEM_RESERVE",
            _ => $"0x{state:X}"
        };
    }

    public string GetTypeText(uint type)
    {
        return type switch
        {
            0x1000000 => "MEM_IMAGE",
            0x40000 => "MEM_MAPPED",
            0x20000 => "MEM_PRIVATE",
            _ => $"0x{type:X}"
        };
    }

    private static bool IsReadableCommitted(NativeMethods.MEMORY_BASIC_INFORMATION mbi)
    {
        if (mbi.State != NativeMethods.MEM_COMMIT)
            return false;

        if ((mbi.Protect & NativeMethods.PAGE_GUARD) != 0)
            return false;

        if ((mbi.Protect & NativeMethods.PAGE_NOACCESS) != 0)
            return false;

        var protection = mbi.Protect & 0xFF;

        return protection is
            NativeMethods.PAGE_READONLY or
            NativeMethods.PAGE_READWRITE or
            NativeMethods.PAGE_WRITECOPY or
            NativeMethods.PAGE_EXECUTE_READ or
            NativeMethods.PAGE_EXECUTE_READWRITE or
            NativeMethods.PAGE_EXECUTE_WRITECOPY;
    }

    private void EnsureAttached()
    {
        if (!IsAttached)
            throw new InvalidOperationException("Belum attach ke process.");
    }

    public void Detach()
    {
        if (_processHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }

        AttachedProcessId = null;
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
