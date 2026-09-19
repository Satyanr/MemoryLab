using System.Globalization;
using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class MemoryScanner
{
    private const int ChunkSize = 1024 * 1024;
    public int MaxResults { get; set; } = 1_000_000;

    private readonly MemoryService _memory;

    public MemoryScanner(MemoryService memory)
    {
        _memory = memory;
    }

    public Task<List<ScanResult>> FirstScanInt32Async(
        ScanMode mode,
        int? value1,
        int? value2,
        bool aligned,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
            FirstScanInt32(
                mode,
                value1,
                value2,
                aligned ? 4 : 1,
                progress,
                cancellationToken),
            cancellationToken);
    }

    public Task<List<ScanResult>> FirstScanFloatAsync(
        ScanMode mode,
        float? value1,
        float? value2,
        bool aligned,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
            FirstScanFloat(
                mode,
                value1,
                value2,
                aligned ? 4 : 1,
                progress,
                cancellationToken),
            cancellationToken);
    }

    public Task<List<ScanResult>> NextScanInt32Async(
        IEnumerable<ScanResult> current,
        ScanMode mode,
        int? value1,
        int? value2,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var source = current.ToList();
            var results = new List<ScanResult>();

            for (var i = 0; i < source.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = source[i];

                if (_memory.TryReadBytes(item.Address, sizeof(int), out var bytes) &&
                    bytes.Length == sizeof(int))
                {
                    var currentValue = BitConverter.ToInt32(bytes, 0);
                    var previousValue = (int)item.NumericValue;

                    if (MatchesInt32Next(mode, previousValue, currentValue, value1, value2))
                    {
                        results.Add(new ScanResult
                        {
                            Address = item.Address,
                            ValueType = "Int32",
                            PreviousNumericValue = previousValue,
                            NumericValue = currentValue,
                            PreviousValue = previousValue.ToString(CultureInfo.InvariantCulture),
                            Value = currentValue.ToString(CultureInfo.InvariantCulture)
                        });

                        if (results.Count >= MaxResults)
                            break;
                    }
                }

                if ((i & 0x1FFF) == 0 || i == source.Count - 1)
                {
                    progress?.Report(
                        source.Count == 0 ? 1 : (double)(i + 1) / source.Count);
                }
            }

            progress?.Report(1);
            return results;
        }, cancellationToken);
    }

    public Task<List<ScanResult>> NextScanFloatAsync(
        IEnumerable<ScanResult> current,
        ScanMode mode,
        float? value1,
        float? value2,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var source = current.ToList();
            var results = new List<ScanResult>();

            for (var i = 0; i < source.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = source[i];

                if (_memory.TryReadBytes(item.Address, sizeof(float), out var bytes) &&
                    bytes.Length == sizeof(float))
                {
                    var currentValue = BitConverter.ToSingle(bytes, 0);
                    var previousValue = (float)item.NumericValue;

                    if (IsUsableFloat(currentValue) &&
                        MatchesFloatNext(mode, previousValue, currentValue, value1, value2))
                    {
                        results.Add(new ScanResult
                        {
                            Address = item.Address,
                            ValueType = "Float",
                            PreviousNumericValue = previousValue,
                            NumericValue = currentValue,
                            PreviousValue = previousValue.ToString("G9", CultureInfo.InvariantCulture),
                            Value = currentValue.ToString("G9", CultureInfo.InvariantCulture)
                        });

                        if (results.Count >= MaxResults)
                            break;
                    }
                }

                if ((i & 0x1FFF) == 0 || i == source.Count - 1)
                {
                    progress?.Report(
                        source.Count == 0 ? 1 : (double)(i + 1) / source.Count);
                }
            }

            progress?.Report(1);
            return results;
        }, cancellationToken);
    }

    private List<ScanResult> FirstScanInt32(
        ScanMode mode,
        int? value1,
        int? value2,
        int alignment,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!IsValidFirstScanMode(mode))
            throw new InvalidOperationException(
                "Scan type ini membutuhkan hasil scan sebelumnya. Gunakan Unknown Initial Value atau scan berbasis nilai terlebih dahulu.");

        var regions = _memory.GetReadableRegions();
        var results = new List<ScanResult>();
        var totalBytes = TotalBytes(regions);
        ulong processedBytes = 0;

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            nuint offset = 0;

            while (offset < region.RegionSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = region.RegionSize - offset;
                var bytesToRead = remaining > (nuint)ChunkSize
                    ? ChunkSize
                    : checked((int)remaining);

                var address = region.BaseAddress + offset;

                if (_memory.TryReadBytes(address, bytesToRead, out var buffer) &&
                    buffer.Length >= sizeof(int))
                {
                    var max = buffer.Length - sizeof(int);

                    for (var i = 0; i <= max; i++)
                    {
                        var absolute = address + (nuint)i;

                        if (alignment > 1 && absolute % (nuint)alignment != 0)
                            continue;

                        var currentValue = BitConverter.ToInt32(buffer, i);

                        if (!MatchesInt32First(mode, currentValue, value1, value2))
                            continue;

                        results.Add(new ScanResult
                        {
                            Address = absolute,
                            ValueType = "Int32",
                            NumericValue = currentValue,
                            PreviousNumericValue = currentValue,
                            PreviousValue = "-",
                            Value = currentValue.ToString(CultureInfo.InvariantCulture)
                        });

                        if (results.Count >= MaxResults)
                            return results;
                    }
                }

                var advance = bytesToRead;

                if (remaining > (nuint)bytesToRead && sizeof(int) > 1)
                    advance -= sizeof(int) - 1;

                offset += (nuint)advance;
                processedBytes += (ulong)advance;
                ReportProgress(progress, processedBytes, totalBytes);
            }
        }

        progress?.Report(1);
        return results;
    }

    private List<ScanResult> FirstScanFloat(
        ScanMode mode,
        float? value1,
        float? value2,
        int alignment,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!IsValidFirstScanMode(mode))
            throw new InvalidOperationException(
                "Scan type ini membutuhkan hasil scan sebelumnya. Gunakan Unknown Initial Value atau scan berbasis nilai terlebih dahulu.");

        var regions = _memory.GetReadableRegions();
        var results = new List<ScanResult>();
        var totalBytes = TotalBytes(regions);
        ulong processedBytes = 0;

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            nuint offset = 0;

            while (offset < region.RegionSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = region.RegionSize - offset;
                var bytesToRead = remaining > (nuint)ChunkSize
                    ? ChunkSize
                    : checked((int)remaining);

                var address = region.BaseAddress + offset;

                if (_memory.TryReadBytes(address, bytesToRead, out var buffer) &&
                    buffer.Length >= sizeof(float))
                {
                    var max = buffer.Length - sizeof(float);

                    for (var i = 0; i <= max; i++)
                    {
                        var absolute = address + (nuint)i;

                        if (alignment > 1 && absolute % (nuint)alignment != 0)
                            continue;

                        var currentValue = BitConverter.ToSingle(buffer, i);

                        if (!IsUsableFloat(currentValue))
                            continue;

                        if (!MatchesFloatFirst(mode, currentValue, value1, value2))
                            continue;

                        results.Add(new ScanResult
                        {
                            Address = absolute,
                            ValueType = "Float",
                            NumericValue = currentValue,
                            PreviousNumericValue = currentValue,
                            PreviousValue = "-",
                            Value = currentValue.ToString("G9", CultureInfo.InvariantCulture)
                        });

                        if (results.Count >= MaxResults)
                            return results;
                    }
                }

                var advance = bytesToRead;

                if (remaining > (nuint)bytesToRead && sizeof(float) > 1)
                    advance -= sizeof(float) - 1;

                offset += (nuint)advance;
                processedBytes += (ulong)advance;
                ReportProgress(progress, processedBytes, totalBytes);
            }
        }

        progress?.Report(1);
        return results;
    }

    private static bool MatchesInt32First(
        ScanMode mode,
        int current,
        int? value1,
        int? value2)
    {
        return mode switch
        {
            ScanMode.UnknownInitialValue => true,
            ScanMode.ExactValue => current == Require(value1),
            ScanMode.GreaterThan => current > Require(value1),
            ScanMode.LessThan => current < Require(value1),
            ScanMode.Between => Between(current, Require(value1), Require(value2)),
            _ => false
        };
    }

    private static bool MatchesInt32Next(
        ScanMode mode,
        int previous,
        int current,
        int? value1,
        int? value2)
    {
        return mode switch
        {
            ScanMode.ExactValue => current == Require(value1),
            ScanMode.ChangedValue => current != previous,
            ScanMode.UnchangedValue => current == previous,
            ScanMode.IncreasedValue => current > previous,
            ScanMode.DecreasedValue => current < previous,
            ScanMode.GreaterThan => current > Require(value1),
            ScanMode.LessThan => current < Require(value1),
            ScanMode.Between => Between(current, Require(value1), Require(value2)),
            ScanMode.IncreasedBy => current == previous + Require(value1),
            ScanMode.DecreasedBy => current == previous - Require(value1),
            ScanMode.UnknownInitialValue => true,
            _ => false
        };
    }

    private static bool MatchesFloatFirst(
        ScanMode mode,
        float current,
        float? value1,
        float? value2)
    {
        return mode switch
        {
            ScanMode.UnknownInitialValue => true,
            ScanMode.ExactValue => FloatEquals(current, Require(value1)),
            ScanMode.GreaterThan => current > Require(value1),
            ScanMode.LessThan => current < Require(value1),
            ScanMode.Between => Between(current, Require(value1), Require(value2)),
            _ => false
        };
    }

    private static bool MatchesFloatNext(
        ScanMode mode,
        float previous,
        float current,
        float? value1,
        float? value2)
    {
        return mode switch
        {
            ScanMode.ExactValue => FloatEquals(current, Require(value1)),
            ScanMode.ChangedValue => !FloatEquals(current, previous),
            ScanMode.UnchangedValue => FloatEquals(current, previous),
            ScanMode.IncreasedValue => current > previous,
            ScanMode.DecreasedValue => current < previous,
            ScanMode.GreaterThan => current > Require(value1),
            ScanMode.LessThan => current < Require(value1),
            ScanMode.Between => Between(current, Require(value1), Require(value2)),
            ScanMode.IncreasedBy => FloatEquals(current, previous + Require(value1)),
            ScanMode.DecreasedBy => FloatEquals(current, previous - Require(value1)),
            ScanMode.UnknownInitialValue => true,
            _ => false
        };
    }

    private static bool IsValidFirstScanMode(ScanMode mode)
    {
        return mode is
            ScanMode.ExactValue or
            ScanMode.UnknownInitialValue or
            ScanMode.GreaterThan or
            ScanMode.LessThan or
            ScanMode.Between;
    }

    private static bool IsUsableFloat(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool FloatEquals(float a, float b)
    {
        if (!IsUsableFloat(a) || !IsUsableFloat(b))
            return false;

        return a.Equals(b);
    }

    private static bool Between<T>(T value, T a, T b)
        where T : IComparable<T>
    {
        var min = a.CompareTo(b) <= 0 ? a : b;
        var max = a.CompareTo(b) <= 0 ? b : a;

        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }

    private static T Require<T>(T? value)
        where T : struct
    {
        return value ?? throw new InvalidOperationException("Scan value belum diisi.");
    }

    private static ulong TotalBytes(IEnumerable<MemoryRegion> regions)
    {
        ulong total = 0;

        foreach (var region in regions)
            total += (ulong)region.RegionSize;

        return total;
    }

    private static void ReportProgress(
        IProgress<double>? progress,
        ulong processedBytes,
        ulong totalBytes)
    {
        if (totalBytes == 0)
        {
            progress?.Report(1);
            return;
        }

        progress?.Report(
            Math.Min(1, (double)processedBytes / totalBytes));
    }
}
