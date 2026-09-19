using System.ComponentModel;
using System.Runtime.InteropServices;
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
            NativeMethods.ProcessAccessFlags.VirtualMemoryRead;

        _processHandle = NativeMethods.OpenProcess(access, false, processId);

        if (_processHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "Gagal membuka process.");
        }

        AttachedProcessId = processId;
    }

    public byte[] ReadBytes(nuint address, int count)
    {
        if (!IsAttached)
            throw new InvalidOperationException("Belum attach ke process.");

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
