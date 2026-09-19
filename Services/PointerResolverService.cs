using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class PointerResolverService
{
    private const int ScanChunkSize = 1024 * 1024;
    private const int MaxPointerScanResults = 50_000;

    private readonly MemoryService _memory;
    private readonly ProcessService _processService;

    public PointerResolverService(
        MemoryService memory,
        ProcessService processService)
    {
        _memory = memory;
        _processService = processService;
    }

    public nuint ResolvePointerChain(
        nuint moduleBase,
        nuint baseOffset,
        IReadOnlyList<nuint> offsets)
    {
        checked
        {
            nuint address = moduleBase + baseOffset;

            foreach (var offset in offsets)
            {
                var pointer = _memory.ReadUInt64(address);

                if (pointer == 0)
                    throw new InvalidOperationException(
                        $"Null pointer ditemukan di 0x{address:X16}.");

                address = (nuint)pointer + offset;
            }

            return address;
        }
    }

    public string FormatModuleRelative(
        int processId,
        nuint address)
    {
        return _processService.FormatModuleRelativeAddress(
            processId,
            address);
    }

    public Task<List<PointerScanResult>> OneLevelPointerScanAsync(
        int processId,
        nuint targetAddress,
        nuint maxOffset,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
            OneLevelPointerScan(
                processId,
                targetAddress,
                maxOffset,
                progress,
                cancellationToken),
            cancellationToken);
    }

    private List<PointerScanResult> OneLevelPointerScan(
        int processId,
        nuint targetAddress,
        nuint maxOffset,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var regions = _memory.GetReadableRegions();
        var results = new List<PointerScanResult>();

        ulong totalBytes = 0;
        foreach (var region in regions)
            totalBytes += (ulong)region.RegionSize;

        ulong processedBytes = 0;

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            nuint offset = 0;

            while (offset < region.RegionSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = region.RegionSize - offset;
                var bytesToRead = remaining > (nuint)ScanChunkSize
                    ? ScanChunkSize
                    : checked((int)remaining);

                var chunkAddress = region.BaseAddress + offset;

                if (_memory.TryReadBytes(
                        chunkAddress,
                        bytesToRead,
                        out var buffer) &&
                    buffer.Length >= sizeof(ulong))
                {
                    var max = buffer.Length - sizeof(ulong);

                    // Pointer candidates are checked at 8-byte aligned positions.
                    for (var i = 0; i <= max; i += sizeof(ulong))
                    {
                        var candidateLocation =
                            chunkAddress + (nuint)i;

                        var pointerValue =
                            BitConverter.ToUInt64(buffer, i);

                        if (pointerValue == 0)
                            continue;

                        var pv = (nuint)pointerValue;

                        if (pv > targetAddress)
                            continue;

                        var candidateOffset =
                            targetAddress - pv;

                        if (candidateOffset > maxOffset)
                            continue;

                        results.Add(new PointerScanResult
                        {
                            PointerAddress = candidateLocation,
                            PointerValue = pv,
                            TargetAddress = targetAddress,
                            Offset = candidateOffset,
                            PointerLocation =
                                _processService.FormatModuleRelativeAddress(
                                    processId,
                                    candidateLocation)
                        });

                        if (results.Count >= MaxPointerScanResults)
                            return results;
                    }
                }

                var advance = bytesToRead;

                if (remaining > (nuint)bytesToRead &&
                    sizeof(ulong) > 1)
                {
                    advance -= sizeof(ulong) - 1;
                }

                offset += (nuint)advance;
                processedBytes += (ulong)advance;

                if (totalBytes > 0)
                {
                    progress?.Report(
                        Math.Min(
                            1,
                            (double)processedBytes /
                            totalBytes));
                }
            }
        }

        progress?.Report(1);
        return results;
    }
}
