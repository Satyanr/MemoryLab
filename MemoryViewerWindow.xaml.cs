using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using MemoryLab.Models;
using MemoryLab.Services;

namespace MemoryLab;

public partial class MemoryViewerWindow : Window
{
    private const int BytesPerRow = 16;
    private const int RowsPerPage = 32;
    private const int PageSize = BytesPerRow * RowsPerPage;

    private readonly MemoryService _memoryService;
    private readonly ObservableCollection<HexRow> _rows = new();
    private readonly DispatcherTimer _refreshTimer;

    private nuint _currentAddress;
    private bool _refreshBusy;

    public MemoryViewerWindow(
        MemoryService memoryService,
        nuint initialAddress,
        int refreshIntervalMs = 500)
    {
        InitializeComponent();

        _memoryService = memoryService;
        _currentAddress = AlignDown(initialAddress, BytesPerRow);

        HexGrid.ItemsSource = _rows;
        AddressTextBox.Text = $"0x{_currentAddress:X16}";

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                Math.Clamp(refreshIntervalMs, 100, 5000))
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        Loaded += MemoryViewerWindow_Loaded;
        Closed += MemoryViewerWindow_Closed;
    }

    private void MemoryViewerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshPage();
    }

    private void MemoryViewerWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _currentAddress = AlignDown(ParseAddress(AddressTextBox.Text), BytesPerRow);
            AddressTextBox.Text = $"0x{_currentAddress:X16}";
            RefreshPage();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAddress >= (nuint)PageSize)
            _currentAddress -= (nuint)PageSize;
        else
            _currentAddress = 0;

        AddressTextBox.Text = $"0x{_currentAddress:X16}";
        RefreshPage();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        var next = _currentAddress + (nuint)PageSize;

        if (next > _currentAddress)
            _currentAddress = next;

        AddressTextBox.Text = $"0x{_currentAddress:X16}";
        RefreshPage();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_refreshBusy ||
            LiveRefreshCheckBox.IsChecked != true ||
            !_memoryService.IsAttached)
        {
            return;
        }

        _refreshBusy = true;

        try
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(RefreshPage);
            });
        }
        finally
        {
            _refreshBusy = false;
        }
    }

    private void RefreshPage()
    {
        if (!_memoryService.IsAttached)
        {
            ShowError("Process tidak lagi ter-attach.");
            return;
        }

        UpdateRegionInfo();

        if (!_memoryService.TryReadBytes(_currentAddress, PageSize, out var bytes) ||
            bytes.Length == 0)
        {
            _rows.Clear();
            StatusText.Text =
                $"Tidak bisa membaca memory mulai 0x{_currentAddress:X16}.";
            return;
        }

        var newRows = new List<HexRow>();

        for (var offset = 0; offset < bytes.Length; offset += BytesPerRow)
        {
            var rowBytes = new byte[BytesPerRow];
            var available = Math.Min(BytesPerRow, bytes.Length - offset);

            Array.Copy(bytes, offset, rowBytes, 0, available);

            var hex = Enumerable.Range(0, BytesPerRow)
                .Select(i => i < available ? rowBytes[i].ToString("X2") : string.Empty)
                .ToArray();

            var ascii = new StringBuilder(BytesPerRow);

            for (var i = 0; i < available; i++)
            {
                var b = rowBytes[i];
                ascii.Append(b is >= 32 and <= 126 ? (char)b : '.');
            }

            while (ascii.Length < BytesPerRow)
                ascii.Append(' ');

            newRows.Add(new HexRow
            {
                Address = $"0x{(_currentAddress + (nuint)offset):X16}",
                Hex00 = hex[0],
                Hex01 = hex[1],
                Hex02 = hex[2],
                Hex03 = hex[3],
                Hex04 = hex[4],
                Hex05 = hex[5],
                Hex06 = hex[6],
                Hex07 = hex[7],
                Hex08 = hex[8],
                Hex09 = hex[9],
                Hex0A = hex[10],
                Hex0B = hex[11],
                Hex0C = hex[12],
                Hex0D = hex[13],
                Hex0E = hex[14],
                Hex0F = hex[15],
                Ascii = ascii.ToString()
            });
        }

        _rows.Clear();

        foreach (var row in newRows)
            _rows.Add(row);

        StatusText.Text =
            $"Read {bytes.Length:N0} bytes from 0x{_currentAddress:X16}.";
    }

    private void UpdateRegionInfo()
    {
        try
        {
            var region = _memoryService.GetRegionForAddress(_currentAddress);

            if (region is null)
            {
                RegionBaseText.Text = "-";
                RegionSizeText.Text = "-";
                RegionStateTypeText.Text = "-";
                RegionProtectionText.Text = "-";
                return;
            }

            RegionBaseText.Text = $"0x{region.BaseAddress:X16}";
            RegionSizeText.Text =
                $"{region.RegionSize:N0} bytes (0x{region.RegionSize:X})";

            RegionStateTypeText.Text =
                $"{_memoryService.GetStateText(region.State)} / " +
                $"{_memoryService.GetTypeText(region.Type)}";

            RegionProtectionText.Text =
                _memoryService.GetProtectionText(region.Protect);
        }
        catch
        {
            RegionBaseText.Text = "-";
            RegionSizeText.Text = "-";
            RegionStateTypeText.Text = "-";
            RegionProtectionText.Text = "-";
        }
    }

    private static nuint ParseAddress(string raw)
    {
        raw = raw.Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            raw = raw[2..];

        if (!ulong.TryParse(
                raw,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new FormatException(
                "Address harus hexadecimal, contoh: 0x7FF612341000.");
        }

        return checked((nuint)value);
    }

    private static nuint AlignDown(nuint address, int alignment)
    {
        var mask = (nuint)(alignment - 1);
        return address & ~mask;
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
    }
}
