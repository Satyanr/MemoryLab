using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using MemoryLab.Models;
using MemoryLab.Services;

namespace MemoryLab;

public partial class PointerToolsWindow : Window
{
    private readonly int _processId;
    private readonly string _processName;
    private readonly MemoryService _memoryService;
    private readonly ProcessService _processService;
    private readonly PointerResolverService _pointerService;

    private readonly ObservableCollection<PointerScanResult> _pointerResults = new();

    private CancellationTokenSource? _pointerScanCancellation;
    private nuint? _resolvedAddress;
    private ModuleInfo? _resolvedModule;
    private nuint _resolvedBaseOffset;
    private IReadOnlyList<nuint> _resolvedOffsets = Array.Empty<nuint>();
    private readonly Action<AddressEntry>? _saveAddressCallback;

    public PointerToolsWindow(
        int processId,
        string processName,
        MemoryService memoryService,
        ProcessService processService,
        nuint? initialAddress = null,
        Action<AddressEntry>? saveAddressCallback = null)
    {
        InitializeComponent();

        _processId = processId;
        _processName = processName;
        _memoryService = memoryService;
        _processService = processService;
        _pointerService = new PointerResolverService(
            memoryService,
            processService);

        _saveAddressCallback = saveAddressCallback;

        PointerResultsGrid.ItemsSource = _pointerResults;

        ProcessText.Text =
            $"{_processName}.exe — PID {_processId}";

        if (initialAddress.HasValue)
        {
            var text = $"0x{initialAddress.Value:X16}";
            AddressTextBox.Text = text;
            PointerTargetTextBox.Text = text;
        }

        Loaded += PointerToolsWindow_Loaded;
        Closed += PointerToolsWindow_Closed;
    }

    private void PointerToolsWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        RefreshModules();
    }

    private void PointerToolsWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _pointerScanCancellation?.Cancel();
        _pointerScanCancellation?.Dispose();
    }

    private void RefreshModules_Click(
        object sender,
        RoutedEventArgs e)
    {
        RefreshModules();
    }

    private void RefreshModules()
    {
        try
        {
            var modules = _processService.GetModules(_processId);
            ModuleComboBox.ItemsSource = modules;

            if (modules.Count > 0)
                ModuleComboBox.SelectedIndex = 0;

            StatusText.Text =
                $"{modules.Count:N0} modules loaded.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ResolvePointer_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ModuleComboBox.SelectedItem is not ModuleInfo module)
        {
            MessageBox.Show(
                "Pilih module terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var baseOffset = ParseHex(BaseOffsetTextBox.Text);
            var offsets = ParseOffsets(OffsetsTextBox.Text);

            var resolved =
                _pointerService.ResolvePointerChain(
                    module.BaseAddress,
                    baseOffset,
                    offsets);

            _resolvedAddress = resolved;
            _resolvedModule = module;
            _resolvedBaseOffset = baseOffset;
            _resolvedOffsets = offsets;

            ResolvedAddressText.Text =
                $"0x{resolved:X16}";

            ResolvedRelativeText.Text =
                _pointerService.FormatModuleRelative(
                    _processId,
                    resolved);

            AddressTextBox.Text =
                $"0x{resolved:X16}";

            PointerTargetTextBox.Text =
                $"0x{resolved:X16}";

            StatusText.Text =
                "Pointer chain resolved.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }


    private void SaveResolvedPointer_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_resolvedAddress.HasValue ||
            _resolvedModule is null)
        {
            MessageBox.Show(
                "Resolve pointer chain terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_saveAddressCallback is null)
        {
            MessageBox.Show(
                "Address List callback tidak tersedia.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var offsetsText =
            string.Join(
                ",",
                _resolvedOffsets.Select(x => $"0x{x:X}"));

        var expression =
            $"{_resolvedModule.Name}|0x{_resolvedBaseOffset:X}|{offsetsText}";

        _saveAddressCallback(
            new AddressEntry
            {
                Address = _resolvedAddress.Value,
                AddressKind = "PointerChain",
                AddressExpression = expression,
                Description = $"Pointer {_resolvedModule.Name}+0x{_resolvedBaseOffset:X}",
                Group = "Pointers",
                ValueType = "Int32",
                Value = "<refreshing>",
                FrozenValue = string.Empty
            });

        StatusText.Text =
            "Resolved pointer saved to Address List.";
    }

    private void ReadResolvedInt32_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_resolvedAddress.HasValue)
            return;

        try
        {
            var value =
                _memoryService.ReadInt32(
                    _resolvedAddress.Value);

            ResolvedValueText.Text =
                $"Value: {value}";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ReadResolvedFloat_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_resolvedAddress.HasValue)
            return;

        try
        {
            var value =
                _memoryService.ReadFloat(
                    _resolvedAddress.Value);

            ResolvedValueText.Text =
                $"Value: {value.ToString("G9", CultureInfo.InvariantCulture)}";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ConvertAddress_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var address = ParseHex(AddressTextBox.Text);

            ModuleRelativeText.Text =
                _pointerService.FormatModuleRelative(
                    _processId,
                    address);

            StatusText.Text =
                "Address converted.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void PointerScan_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var targetAddress =
                ParseHex(PointerTargetTextBox.Text);

            var maxOffset =
                ParseHex(MaxOffsetTextBox.Text);

            SetPointerScanState(true);

            _pointerResults.Clear();
            PointerResultCountText.Text = "0 results";

            _pointerScanCancellation =
                new CancellationTokenSource();

            var progress = new Progress<double>(p =>
            {
                PointerScanProgressBar.Value =
                    Math.Clamp(p * 100, 0, 100);

                PointerScanInfoText.Text =
                    $"Scanning... {PointerScanProgressBar.Value:0}%";
            });

            var results =
                await _pointerService.OneLevelPointerScanAsync(
                    _processId,
                    targetAddress,
                    maxOffset,
                    progress,
                    _pointerScanCancellation.Token);

            foreach (var result in results)
                _pointerResults.Add(result);

            PointerResultCountText.Text =
                $"{_pointerResults.Count:N0} results";

            PointerScanInfoText.Text =
                _pointerResults.Count >= 50_000
                    ? "Result cap reached."
                    : "Pointer scan complete.";

            StatusText.Text =
                "One-level pointer scan complete.";
        }
        catch (OperationCanceledException)
        {
            PointerScanInfoText.Text =
                "Pointer scan cancelled.";
            StatusText.Text =
                "Pointer scan cancelled.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetPointerScanState(false);
        }
    }

    private void CancelPointerScan_Click(
        object sender,
        RoutedEventArgs e)
    {
        _pointerScanCancellation?.Cancel();
    }

    private void SetPointerScanState(bool scanning)
    {
        PointerScanButton.IsEnabled = !scanning;
        CancelPointerScanButton.IsEnabled = scanning;

        if (!scanning)
        {
            _pointerScanCancellation?.Dispose();
            _pointerScanCancellation = null;
        }
    }

    private static nuint ParseHex(string raw)
    {
        raw = raw.Trim();

        if (raw.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[2..];
        }

        if (!ulong.TryParse(
                raw,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new FormatException(
                "Nilai hexadecimal tidak valid.");
        }

        return checked((nuint)value);
    }

    private static IReadOnlyList<nuint> ParseOffsets(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<nuint>();

        return raw
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(ParseHex)
            .ToList();
    }

    private void ShowError(Exception ex)
    {
        StatusText.Text = ex.Message;

        MessageBox.Show(
            ex.Message,
            "MemoryLab Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
