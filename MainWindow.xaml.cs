using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using MemoryLab.Models;
using MemoryLab.Services;

namespace MemoryLab;

public partial class MainWindow : Window
{
    private readonly ProcessService _processService = new();
    private readonly MemoryService _memoryService = new();
    private readonly MemoryScanner _scanner;
    private readonly ProjectService _projectService = new();
    private readonly RecentProjectService _recentProjectService = new();
    private readonly AppSettingsService _appSettingsService = new();
    private AppSettings _appSettings = new();

    private string? _currentProjectPath;
    private string _projectName = "MemoryLab Project";
    private string _projectProcessName = string.Empty;

    private readonly BulkObservableCollection<ScanResult> _scanResults = new();
    private readonly ObservableCollection<AddressEntry> _addressEntries = new();

    private readonly DispatcherTimer _refreshTimer;

    private ProcessItem? _selectedProcess;
    private CancellationTokenSource? _scanCancellation;
    private string? _activeScanType;
    private bool _hasScanSnapshot;
    private bool _refreshBusy;
    private DateTime _lastProcessHealthCheckUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        _appSettings = _appSettingsService.Load();

        _scanner = new MemoryScanner(_memoryService)
        {
            MaxResults = _appSettings.ScanResultLimit
        };

        ResultsGrid.ItemsSource = _scanResults;
        AddressGrid.ItemsSource = _addressEntries;

        PreviewKeyDown += MainWindow_PreviewKeyDown;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                _appSettings.AddressRefreshMs)
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
        UpdateScanModeUi();
        UpdateProjectStatus();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();

        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();

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
            ProcessCountText.Text = $"{processes.Count:N0}";
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

            using var process = _processService.TryGetProcess(_selectedProcess.Id)
                ?? throw new InvalidOperationException("Process sudah tidak berjalan.");

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
            }

            AttachedText.Text =
                $"{_selectedProcess.Name}.exe\n" +
                $"PID: {_selectedProcess.Id}\n" +
                $"{moduleText}\n" +
                $"{baseAddressText}";

            NewScanInternal();
            RefreshSavedAddressValues();
            UpdateAddressCount();

            StatusText.Text =
                $"Attached to {_selectedProcess.Name}.exe (PID {_selectedProcess.Id}).";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ScanModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateScanModeUi();
    }

    private void UpdateScanModeUi()
    {
        var mode = GetSelectedScanMode();

        var needsNoValue = mode is
            ScanMode.UnknownInitialValue or
            ScanMode.ChangedValue or
            ScanMode.UnchangedValue or
            ScanMode.IncreasedValue or
            ScanMode.DecreasedValue;

        var needsSecondValue = mode == ScanMode.Between;

        Value1Label.Visibility =
            needsNoValue ? Visibility.Collapsed : Visibility.Visible;

        ScanValueTextBox.Visibility =
            needsNoValue ? Visibility.Collapsed : Visibility.Visible;

        Value2Label.Visibility =
            needsSecondValue ? Visibility.Visible : Visibility.Collapsed;

        ScanValue2TextBox.Visibility =
            needsSecondValue ? Visibility.Visible : Visibility.Collapsed;

        Value1Label.Text = mode switch
        {
            ScanMode.IncreasedBy => "Increase amount",
            ScanMode.DecreasedBy => "Decrease amount",
            ScanMode.GreaterThan => "Greater than",
            ScanMode.LessThan => "Less than",
            ScanMode.Between => "Minimum / first value",
            _ => "Value"
        };

        Value2Label.Text = "Maximum / second value";
    }

    private async void FirstScan_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanScan())
            return;

        var scanType = GetSelectedScanType();
        var mode = GetSelectedScanMode();

        if (mode is
            ScanMode.ChangedValue or
            ScanMode.UnchangedValue or
            ScanMode.IncreasedValue or
            ScanMode.DecreasedValue or
            ScanMode.IncreasedBy or
            ScanMode.DecreasedBy)
        {
            MessageBox.Show(
                "Scan type ini membutuhkan snapshot sebelumnya. Mulai dengan Unknown Initial Value, Exact Value, Greater Than, Less Than, atau Between.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            SetScanningState(true);

            _scanCancellation = new CancellationTokenSource();
            var progress = CreateProgress();

            List<ScanResult> results;

            if (scanType == "Int32")
            {
                var (value1, value2) = ParseIntValues(mode);

                results = await _scanner.FirstScanInt32Async(
                    mode,
                    value1,
                    value2,
                    AlignedCheckBox.IsChecked == true,
                    progress,
                    _scanCancellation.Token);
            }
            else
            {
                var (value1, value2) = ParseFloatValues(mode);

                results = await _scanner.FirstScanFloatAsync(
                    mode,
                    value1,
                    value2,
                    AlignedCheckBox.IsChecked == true,
                    progress,
                    _scanCancellation.Token);
            }

            ApplyResults(results);

            _activeScanType = scanType;
            _hasScanSnapshot = true;
            NextScanButton.IsEnabled = _scanResults.Count > 0;

            var capped =
                _scanResults.Count >= 1_000_000
                    ? " Result cap reached."
                    : string.Empty;

            ScanInfoText.Text =
                $"{_scanResults.Count:N0} address found.{capped}";

            StatusText.Text = "First Scan complete.";
        }
        catch (OperationCanceledException)
        {
            ScanInfoText.Text = "Scan cancelled.";
            StatusText.Text = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetScanningState(false);
        }
    }

    private async void NextScan_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanScan() || !_hasScanSnapshot || _scanResults.Count == 0)
            return;

        var scanType = GetSelectedScanType();
        var mode = GetSelectedScanMode();

        if (!string.Equals(scanType, _activeScanType, StringComparison.Ordinal))
        {
            MessageBox.Show(
                "Value Type tidak boleh berubah saat Next Scan. Gunakan New Scan terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            SetScanningState(true);

            _scanCancellation = new CancellationTokenSource();
            var progress = CreateProgress();
            var source = _scanResults.ToList();

            List<ScanResult> results;

            if (scanType == "Int32")
            {
                var (value1, value2) = ParseIntValues(mode);

                results = await _scanner.NextScanInt32Async(
                    source,
                    mode,
                    value1,
                    value2,
                    progress,
                    _scanCancellation.Token);
            }
            else
            {
                var (value1, value2) = ParseFloatValues(mode);

                results = await _scanner.NextScanFloatAsync(
                    source,
                    mode,
                    value1,
                    value2,
                    progress,
                    _scanCancellation.Token);
            }

            ApplyResults(results);

            NextScanButton.IsEnabled = _scanResults.Count > 0;
            ScanInfoText.Text = $"{_scanResults.Count:N0} address remain.";
            StatusText.Text = "Next Scan complete.";
        }
        catch (OperationCanceledException)
        {
            ScanInfoText.Text = "Scan cancelled.";
            StatusText.Text = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetScanningState(false);
        }
    }

    private (int? value1, int? value2) ParseIntValues(ScanMode mode)
    {
        if (!ModeNeedsValue(mode))
            return (null, null);

        if (!int.TryParse(
                ScanValueTextBox.Text.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value1))
        {
            throw new FormatException("Value Int32 pertama tidak valid.");
        }

        if (mode != ScanMode.Between)
            return (value1, null);

        if (!int.TryParse(
                ScanValue2TextBox.Text.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value2))
        {
            throw new FormatException("Value Int32 kedua tidak valid.");
        }

        return (value1, value2);
    }

    private (float? value1, float? value2) ParseFloatValues(ScanMode mode)
    {
        if (!ModeNeedsValue(mode))
            return (null, null);

        if (!float.TryParse(
                ScanValueTextBox.Text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value1))
        {
            throw new FormatException(
                "Value Float pertama tidak valid. Gunakan format seperti 12.5.");
        }

        if (mode != ScanMode.Between)
            return (value1, null);

        if (!float.TryParse(
                ScanValue2TextBox.Text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value2))
        {
            throw new FormatException("Value Float kedua tidak valid.");
        }

        return (value1, value2);
    }

    private static bool ModeNeedsValue(ScanMode mode)
    {
        return mode is
            ScanMode.ExactValue or
            ScanMode.GreaterThan or
            ScanMode.LessThan or
            ScanMode.Between or
            ScanMode.IncreasedBy or
            ScanMode.DecreasedBy;
    }

    private void NewScan_Click(object sender, RoutedEventArgs e)
    {
        NewScanInternal();
    }

    private void NewScanInternal()
    {
        _scanResults.Clear();
        _activeScanType = null;
        _hasScanSnapshot = false;

        NextScanButton.IsEnabled = false;
        ScanProgressBar.Value = 0;
        ResultCountText.Text = "0 results";
        ScanInfoText.Text = "Ready for First Scan.";
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _scanCancellation?.Cancel();
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AddSelectedAddresses();
    }

    private void AddSelectedAddress_Click(object sender, RoutedEventArgs e)
    {
        AddSelectedAddresses();
    }

    private void AddSelectedAddresses()
    {
        if (!_memoryService.IsAttached)
            return;

        var selected = ResultsGrid.SelectedItems.Cast<ScanResult>().ToList();

        if (selected.Count == 0 && ResultsGrid.SelectedItem is ScanResult single)
            selected.Add(single);

        foreach (var result in selected)
        {
            if (_addressEntries.Any(x =>
                    x.Address == result.Address &&
                    string.Equals(x.ValueType, result.ValueType, StringComparison.Ordinal)))
            {
                continue;
            }

            _addressEntries.Add(new AddressEntry
            {
                Address = result.Address,
                AddressKind = "Absolute",
                AddressExpression = result.AddressText,
                ValueType = result.ValueType,
                Description = $"Address {_addressEntries.Count + 1}",
                Group = "Default",
                Value = result.Value,
                FrozenValue = result.Value
            });
        }

        UpdateAddressCount();
        StatusText.Text =
            $"{selected.Count:N0} selected result(s) added to Address List.";
    }




    private void NewProject_Click(
        object sender,
        RoutedEventArgs e)
    {
        _currentProjectPath = null;
        _projectName = "MemoryLab Project";
        _projectProcessName = string.Empty;

        _addressEntries.Clear();
        UpdateAddressCount();
        NewScanInternal();

        UpdateProjectStatus();
        StatusText.Text = "New project created.";
    }

    private async void OpenProject_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open MemoryLab Project",
            Filter = "MemoryLab Project (*.mlab.json)|*.mlab.json|JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        await LoadProjectAsync(dialog.FileName);
    }

    private async void SaveProject_Click(
        object sender,
        RoutedEventArgs e)
    {
        await SaveProjectAsync();
    }

    private async Task SaveProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save MemoryLab Project",
                Filter = "MemoryLab Project (*.mlab.json)|*.mlab.json",
                DefaultExt = ".mlab.json",
                FileName = SanitizeFileName(_projectName) + ".mlab.json"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            _currentProjectPath = dialog.FileName;
        }

        var project = BuildProjectFile();

        await _projectService.SaveAsync(
            _currentProjectPath!,
            project);

        _recentProjectService.Add(_currentProjectPath!);
        UpdateProjectStatus();

        StatusText.Text =
            $"Project saved: {_currentProjectPath}";
    }

    private ProjectFile BuildProjectFile()
    {
        var project = new ProjectFile
        {
            ProjectName = _projectName,
            ProcessName = _projectProcessName
        };

        foreach (var entry in _addressEntries)
        {
            project.Addresses.Add(new ProjectAddressEntry
            {
                Description = entry.Description,
                Group = entry.Group,
                ValueType = entry.ValueType,
                AddressKind = entry.AddressKind,
                AddressExpression = entry.AddressExpression,
                AbsoluteAddress = (ulong)entry.Address,
                Frozen = entry.IsFrozen,
                FrozenValue = entry.FrozenValue
            });
        }

        return project;
    }

    private async Task LoadProjectAsync(string filePath)
    {
        try
        {
            var project =
                await _projectService.LoadAsync(filePath);

            _currentProjectPath = filePath;
            _projectName =
                string.IsNullOrWhiteSpace(project.ProjectName)
                    ? "MemoryLab Project"
                    : project.ProjectName;

            _projectProcessName =
                project.ProcessName ?? string.Empty;

            _addressEntries.Clear();

            foreach (var saved in project.Addresses)
            {
                var address = checked((nuint)saved.AbsoluteAddress);

                _addressEntries.Add(new AddressEntry
                {
                    Address = address,
                    AddressKind = saved.AddressKind,
                    AddressExpression = saved.AddressExpression,
                    Description = saved.Description,
                    Group = string.IsNullOrWhiteSpace(saved.Group)
                        ? "Default"
                        : saved.Group,
                    ValueType = saved.ValueType,
                    Value = "<not attached>",
                    FrozenValue = saved.FrozenValue,
                    IsFrozen = false
                });
            }

            UpdateAddressCount();
            UpdateProjectStatus();

            _recentProjectService.Add(filePath);

            StatusText.Text =
                $"Project loaded: {filePath}";

            if (!string.IsNullOrWhiteSpace(_projectProcessName))
                TryAutoAttachProjectProcess();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ProjectSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new ProjectSettingsWindow(
            _projectName,
            _projectProcessName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        _projectName = dialog.ProjectName;
        _projectProcessName = dialog.ProcessName;

        UpdateProjectStatus();
        StatusText.Text = "Project settings updated.";
    }

    private void AutoAttach_Click(
        object sender,
        RoutedEventArgs e)
    {
        TryAutoAttachProjectProcess();
    }

    private void TryAutoAttachProjectProcess()
    {
        if (string.IsNullOrWhiteSpace(_projectProcessName))
        {
            MessageBox.Show(
                "Set process name di Project Settings terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var item =
            _processService.FindProcessByName(
                _projectProcessName);

        if (item is null)
        {
            StatusText.Text =
                $"Process '{_projectProcessName}' belum berjalan.";
            return;
        }

        _selectedProcess = item;

        try
        {
            _memoryService.Attach(item.Id);

            AttachedText.Text =
                $"{item.Name}.exe\nPID: {item.Id}\nAuto-attached";

            RefreshSavedAddressValues();

            StatusText.Text =
                $"Auto-attached to {item.Name}.exe (PID {item.Id}).";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RefreshSavedAddressValues()
    {
        if (!_memoryService.AttachedProcessId.HasValue)
            return;

        foreach (var entry in _addressEntries)
        {
            try
            {
                ResolveSavedEntryAddress(entry);

                entry.Value =
                    _memoryService.ReadValueAsString(
                        entry.Address,
                        entry.ValueType);

                if (string.IsNullOrWhiteSpace(entry.FrozenValue))
                    entry.FrozenValue = entry.Value;
            }
            catch
            {
                entry.Value = "<unresolved>";
            }
        }
    }

    private void ResolveSavedEntryAddress(AddressEntry entry)
    {
        if (!_memoryService.AttachedProcessId.HasValue)
            return;

        var processId =
            _memoryService.AttachedProcessId.Value;

        if (string.Equals(
                entry.AddressKind,
                "ModuleOffset",
                StringComparison.OrdinalIgnoreCase))
        {
            var parts =
                entry.AddressExpression.Split(
                    '+',
                    2,
                    StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
                throw new FormatException(
                    "Module+offset expression tidak valid.");

            var module =
                _processService.FindModuleByName(
                    processId,
                    parts[0])
                ?? throw new InvalidOperationException(
                    $"Module '{parts[0]}' tidak ditemukan.");

            var offset = ParseHexExpression(parts[1]);
            entry.Address = checked(module.BaseAddress + offset);
            entry.NotifyAddressChanged();
            return;
        }

        if (string.Equals(
                entry.AddressKind,
                "PointerChain",
                StringComparison.OrdinalIgnoreCase))
        {
            var parts =
                entry.AddressExpression.Split('|');

            if (parts.Length != 3)
                throw new FormatException(
                    "Pointer expression tidak valid.");

            var module =
                _processService.FindModuleByName(
                    processId,
                    parts[0])
                ?? throw new InvalidOperationException(
                    $"Module '{parts[0]}' tidak ditemukan.");

            var baseOffset =
                ParseHexExpression(parts[1]);

            var offsets =
                string.IsNullOrWhiteSpace(parts[2])
                    ? Array.Empty<nuint>()
                    : parts[2]
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries)
                        .Select(ParseHexExpression)
                        .ToArray();

            var resolver =
                new PointerResolverService(
                    _memoryService,
                    _processService);

            entry.Address =
                resolver.ResolvePointerChain(
                    module.BaseAddress,
                    baseOffset,
                    offsets);

            entry.NotifyAddressChanged();
        }
    }

    private static nuint ParseHexExpression(string raw)
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
                "Hex expression tidak valid.");
        }

        return checked((nuint)value);
    }

    private void UpdateProjectStatus()
    {
        var filePart =
            string.IsNullOrWhiteSpace(_currentProjectPath)
                ? "Unsaved"
                : Path.GetFileName(_currentProjectPath);

        ProjectStatusText.Text =
            $"{_projectName} • {filePart}";
    }

    private async void MainWindow_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.S)
        {
            e.Handled = true;
            await SaveProjectAsync();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.O)
        {
            e.Handled = true;
            OpenProject_Click(sender, new RoutedEventArgs());
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.N)
        {
            e.Handled = true;
            NewProject_Click(sender, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.F5)
        {
            e.Handled = true;
            TryAutoAttachProjectProcess();
            return;
        }

        if (e.Key == Key.F6 &&
            AddressGrid.SelectedItem is AddressEntry entry)
        {
            e.Handled = true;

            if (!entry.IsFrozen)
            {
                entry.FrozenValue = entry.Value;
                entry.IsFrozen = true;
            }
            else
            {
                entry.IsFrozen = false;
            }

            StatusText.Text = "Freeze toggled with F6.";
        }
    }

    private static string SanitizeFileName(string raw)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            raw = raw.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(raw)
            ? "MemoryLabProject"
            : raw;
    }


    private async void RecentProjects_Click(
        object sender,
        RoutedEventArgs e)
    {
        var recent = _recentProjectService.Load();

        if (recent.Count == 0)
        {
            MessageBox.Show(
                "Belum ada recent project.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var window = new RecentProjectsWindow(recent)
        {
            Owner = this
        };

        if (window.ShowDialog() == true &&
            !string.IsNullOrWhiteSpace(window.SelectedPath))
        {
            await LoadProjectAsync(window.SelectedPath);
        }
    }

    private void Settings_Click(
        object sender,
        RoutedEventArgs e)
    {
        var window = new AppSettingsWindow(_appSettings)
        {
            Owner = this
        };

        if (window.ShowDialog() != true)
            return;

        _appSettings = window.Settings;
        _appSettingsService.Save(_appSettings);

        _scanner.MaxResults =
            _appSettings.ScanResultLimit;

        _refreshTimer.Interval =
            TimeSpan.FromMilliseconds(
                _appSettings.AddressRefreshMs);

        StatusText.Text =
            "Settings saved.";
    }

    private void About_Click(
        object sender,
        RoutedEventArgs e)
    {
        new AboutWindow
        {
            Owner = this
        }.ShowDialog();
    }

    private void MakeModuleRelative_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AddressGrid.SelectedItem is not AddressEntry entry ||
            !_memoryService.AttachedProcessId.HasValue)
        {
            return;
        }

        try
        {
            var module =
                _processService.FindContainingModule(
                    _memoryService.AttachedProcessId.Value,
                    entry.Address);

            if (module is null)
            {
                MessageBox.Show(
                    "Address ini tidak berada di dalam loaded module.",
                    "MemoryLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var offset =
                entry.Address - module.BaseAddress;

            entry.AddressKind = "ModuleOffset";
            entry.AddressExpression =
                $"{module.Name}+0x{offset:X}";

            StatusText.Text =
                $"Saved as {entry.AddressExpression}.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void PointerTools_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenPointerTools(null);
    }

    private void PointerFromResult_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not ScanResult result)
        {
            MessageBox.Show(
                "Pilih salah satu hasil scan terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenPointerTools(result.Address);
    }

    private void PointerFromAddress_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AddressGrid.SelectedItem is not AddressEntry entry)
        {
            MessageBox.Show(
                "Pilih address terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenPointerTools(entry.Address);
    }

    private void OpenPointerTools(nuint? initialAddress)
    {
        if (!_memoryService.IsAttached ||
            !_memoryService.AttachedProcessId.HasValue)
        {
            MessageBox.Show(
                "Attach ke process terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var processName =
            _selectedProcess?.Name ?? "Process";

        var window = new PointerToolsWindow(
            _memoryService.AttachedProcessId.Value,
            processName,
            _memoryService,
            _processService,
            initialAddress,
            AddResolvedAddressEntry)
        {
            Owner = this
        };

        window.Show();
    }


    private void AddResolvedAddressEntry(AddressEntry entry)
    {
        if (_addressEntries.Any(x =>
                x.AddressKind == entry.AddressKind &&
                string.Equals(
                    x.AddressExpression,
                    entry.AddressExpression,
                    StringComparison.OrdinalIgnoreCase)))
        {
            StatusText.Text =
                "Pointer entry already exists in Address List.";
            return;
        }

        try
        {
            if (_memoryService.IsAttached)
            {
                entry.Value =
                    _memoryService.ReadValueAsString(
                        entry.Address,
                        entry.ValueType);

                if (string.IsNullOrWhiteSpace(entry.FrozenValue))
                    entry.FrozenValue = entry.Value;
            }
        }
        catch
        {
            entry.Value = "<unreadable>";
        }

        _addressEntries.Add(entry);
        UpdateAddressCount();

        StatusText.Text =
            "Pointer entry added to Address List.";
    }

    private void ViewSelectedResult_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not ScanResult result)
        {
            MessageBox.Show(
                "Pilih salah satu hasil scan terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenMemoryViewer(result.Address);
    }

    private void ViewAddress_Click(object sender, RoutedEventArgs e)
    {
        if (AddressGrid.SelectedItem is not AddressEntry entry)
        {
            MessageBox.Show(
                "Pilih address terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenMemoryViewer(entry.Address);
    }

    private void OpenMemoryViewer(nuint address)
    {
        if (!_memoryService.IsAttached)
            return;

        var viewer = new MemoryViewerWindow(
            _memoryService,
            address,
            _appSettings.MemoryViewerRefreshMs)
        {
            Owner = this
        };

        viewer.Show();
    }

    private void AddressGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditSelectedAddress();
    }

    private void EditAddress_Click(object sender, RoutedEventArgs e)
    {
        EditSelectedAddress();
    }

    private void EditSelectedAddress()
    {
        if (AddressGrid.SelectedItem is not AddressEntry entry)
            return;

        if (!_memoryService.IsAttached)
            return;

        try
        {
            var dialog = new EditValueWindow(
                entry.AddressText,
                entry.ValueType,
                entry.Value)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            _memoryService.WriteValueFromString(
                entry.Address,
                entry.ValueType,
                dialog.EditedValue);

            entry.Value = _memoryService.ReadValueAsString(
                entry.Address,
                entry.ValueType);

            if (entry.IsFrozen)
                entry.FrozenValue = entry.Value;

            StatusText.Text =
                $"Value written to {entry.AddressText}.";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ToggleFreeze_Click(object sender, RoutedEventArgs e)
    {
        var selected = AddressGrid.SelectedItems.Cast<AddressEntry>().ToList();

        if (selected.Count == 0 && AddressGrid.SelectedItem is AddressEntry single)
            selected.Add(single);

        foreach (var entry in selected)
        {
            if (!entry.IsFrozen)
            {
                entry.FrozenValue = entry.Value;
                entry.IsFrozen = true;
            }
            else
            {
                entry.IsFrozen = false;
            }
        }

        StatusText.Text = "Freeze state updated.";
    }

    private void RemoveAddress_Click(object sender, RoutedEventArgs e)
    {
        var selected = AddressGrid.SelectedItems.Cast<AddressEntry>().ToList();

        foreach (var entry in selected)
            _addressEntries.Remove(entry);

        if (selected.Count == 0 && AddressGrid.SelectedItem is AddressEntry single)
            _addressEntries.Remove(single);

        UpdateAddressCount();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_memoryService.IsAttached &&
            _memoryService.AttachedProcessId.HasValue &&
            DateTime.UtcNow - _lastProcessHealthCheckUtc >
                TimeSpan.FromSeconds(2))
        {
            _lastProcessHealthCheckUtc = DateTime.UtcNow;

            using var alive =
                _processService.TryGetProcess(
                    _memoryService.AttachedProcessId.Value);

            if (alive is null)
            {
                _memoryService.Detach();
                AttachedText.Text = "Not attached";

                foreach (var entry in _addressEntries)
                    entry.Value = "<process exited>";

                StatusText.Text =
                    "Attached process exited. Use F5 / Auto Attach after it restarts.";
                return;
            }
        }

        if (_refreshBusy ||
            !_memoryService.IsAttached ||
            _addressEntries.Count == 0)
        {
            return;
        }

        _refreshBusy = true;

        try
        {
            var snapshot = _addressEntries.ToList();

            await Task.Run(() =>
            {
                foreach (var entry in snapshot)
                {
                    try
                    {
                        if (entry.IsFrozen)
                        {
                            _memoryService.WriteValueFromString(
                                entry.Address,
                                entry.ValueType,
                                entry.FrozenValue);
                        }

                        var current = _memoryService.ReadValueAsString(
                            entry.Address,
                            entry.ValueType);

                        Dispatcher.Invoke(() => entry.Value = current);
                    }
                    catch
                    {
                        Dispatcher.Invoke(
                            () => entry.Value = "<unreadable>");
                    }
                }
            });
        }
        finally
        {
            _refreshBusy = false;
        }
    }

    private bool EnsureCanScan()
    {
        if (!_memoryService.IsAttached)
        {
            MessageBox.Show(
                "Attach ke process terlebih dahulu.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private string GetSelectedScanType()
    {
        return (ScanValueTypeComboBox.SelectedItem as ComboBoxItem)
            ?.Content
            ?.ToString() ?? "Int32";
    }

    private ScanMode GetSelectedScanMode()
    {
        var raw =
            (ScanModeComboBox.SelectedItem as ComboBoxItem)
            ?.Tag
            ?.ToString();

        return Enum.TryParse<ScanMode>(raw, out var mode)
            ? mode
            : ScanMode.ExactValue;
    }

    private Progress<double> CreateProgress()
    {
        return new Progress<double>(value =>
        {
            ScanProgressBar.Value = Math.Clamp(value * 100, 0, 100);
            ScanInfoText.Text =
                $"Scanning... {ScanProgressBar.Value:0}%";
        });
    }

    private void ApplyResults(IEnumerable<ScanResult> results)
    {
        var materialized = results as IReadOnlyCollection<ScanResult>
            ?? results.ToList();

        _scanResults.ReplaceAll(materialized);

        ResultCountText.Text =
            $"{_scanResults.Count:N0} results";
    }

    private void UpdateAddressCount()
    {
        AddressCountText.Text =
            $"{_addressEntries.Count:N0} entries";
    }

    private void SetScanningState(bool scanning)
    {
        FirstScanButton.IsEnabled = !scanning;

        NextScanButton.IsEnabled =
            !scanning &&
            _scanResults.Count > 0 &&
            _hasScanSnapshot;

        CancelScanButton.IsEnabled = scanning;
        ProcessGrid.IsEnabled = !scanning;
        ScanModeComboBox.IsEnabled = !scanning;
        ScanValueTypeComboBox.IsEnabled = !scanning;
        AlignedCheckBox.IsEnabled = !scanning;

        if (!scanning)
        {
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }
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
