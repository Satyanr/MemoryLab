using System.Windows;
using MemoryLab.Models;

namespace MemoryLab;

public partial class AppSettingsWindow : Window
{
    public AppSettings Settings { get; }

    public AppSettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        Settings = new AppSettings
        {
            AddressRefreshMs = settings.AddressRefreshMs,
            MemoryViewerRefreshMs = settings.MemoryViewerRefreshMs,
            ScanResultLimit = settings.ScanResultLimit
        };

        AddressRefreshTextBox.Text =
            Settings.AddressRefreshMs.ToString();

        ViewerRefreshTextBox.Text =
            Settings.MemoryViewerRefreshMs.ToString();

        ScanLimitTextBox.Text =
            Settings.ScanResultLimit.ToString();
    }

    private void Save_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!int.TryParse(
                AddressRefreshTextBox.Text,
                out var addressRefresh) ||
            addressRefresh < 100 ||
            addressRefresh > 5000)
        {
            ShowRangeError(
                "Address refresh harus 100–5000 ms.");
            return;
        }

        if (!int.TryParse(
                ViewerRefreshTextBox.Text,
                out var viewerRefresh) ||
            viewerRefresh < 100 ||
            viewerRefresh > 5000)
        {
            ShowRangeError(
                "Memory Viewer refresh harus 100–5000 ms.");
            return;
        }

        if (!int.TryParse(
                ScanLimitTextBox.Text,
                out var scanLimit) ||
            scanLimit < 10_000 ||
            scanLimit > 1_000_000)
        {
            ShowRangeError(
                "Scan result limit harus 10,000–1,000,000.");
            return;
        }

        Settings.AddressRefreshMs = addressRefresh;
        Settings.MemoryViewerRefreshMs = viewerRefresh;
        Settings.ScanResultLimit = scanLimit;

        DialogResult = true;
    }

    private void ShowRangeError(string message)
    {
        MessageBox.Show(
            message,
            "MemoryLab",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
