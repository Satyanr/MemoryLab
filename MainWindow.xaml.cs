using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MemoryLab.Models;
using MemoryLab.Services;

namespace MemoryLab;

public partial class MainWindow : Window
{
    private readonly ProcessService _processService = new();
    private readonly MemoryService _memoryService = new();

    private ProcessItem? _selectedProcess;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _memoryService.Dispose();
    }

    private void RefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
    }

    private void RefreshProcesses()
    {
        try
        {
            var processes = _processService.GetProcesses();

            ProcessGrid.ItemsSource = processes;
            ProcessCountText.Text = $"{processes.Count:N0} process";
            StatusText.Text = "Process list refreshed.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ProcessGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProcess = ProcessGrid.SelectedItem as ProcessItem;

        if (_selectedProcess is not null)
        {
            StatusText.Text =
                $"Selected: {_selectedProcess.Name}.exe (PID {_selectedProcess.Id})";
        }
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProcess is null)
        {
            MessageBox.Show(
                "Pilih process terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            _memoryService.Attach(_selectedProcess.Id);

            var process = _processService.TryGetProcess(_selectedProcess.Id);

            if (process is null)
                throw new InvalidOperationException("Process sudah tidak berjalan.");

            using (process)
            {
                var moduleText = "N/A";
                var baseAddressText = "N/A";

                try
                {
                    if (process.MainModule is not null)
                    {
                        moduleText = process.MainModule.ModuleName;
                        baseAddressText =
                            $"0x{process.MainModule.BaseAddress.ToInt64():X16}";
                    }
                }
                catch
                {
                    // Module info bisa ditolak untuk process tertentu.
                }

                AttachedText.Text =
                    $"{_selectedProcess.Name}.exe\n" +
                    $"PID: {_selectedProcess.Id}\n" +
                    $"Main Module: {moduleText}\n" +
                    $"Base Address: {baseAddressText}";
            }

            StatusText.Text =
                $"Attached to {_selectedProcess.Name}.exe (PID {_selectedProcess.Id}).";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ReadMemory_Click(object sender, RoutedEventArgs e)
    {
        if (!_memoryService.IsAttached)
        {
            MessageBox.Show(
                "Attach ke process terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var address = ParseAddress(AddressTextBox.Text);

            var selectedType =
                (ValueTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            MemoryResultText.Text = selectedType switch
            {
                "Int32" =>
                    _memoryService.ReadInt32(address).ToString(CultureInfo.InvariantCulture),

                "Float" =>
                    _memoryService.ReadFloat(address).ToString("G9", CultureInfo.InvariantCulture),

                "Bytes (16)" =>
                    BitConverter.ToString(_memoryService.ReadBytes(address, 16))
                        .Replace("-", " "),

                _ => throw new InvalidOperationException("Tipe data tidak dikenali.")
            };

            StatusText.Text = $"Read successful at 0x{address:X}.";
        }
        catch (Exception ex)
        {
            MemoryResultText.Text = "-";
            ShowError(ex);
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
            throw new FormatException("Address harus berupa hexadecimal, contoh: 0x7FF612341000.");
        }

        return checked((nuint)value);
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
