using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MemoryLab;
using MemoryLab.Models;
using MemoryLab.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            CheckFiltering();
            CheckLiveDiscovery();
            CheatTableImportChecks.Run();
            CheckLayout(args.Contains("--preview"));
            Console.WriteLine("PASS: game filtering, CT import, live discovery, and WPF layout checks.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckFiltering()
    {
        var checks = 0;
        void Check(string label, string name, string title, string? path, bool visible, bool expected,
            params string[] modules)
        {
            Assert(GameProcessFilter.IsGame(name, title, path, visible, modules) == expected, label);
            checks++;
        }

        const string steamPath = @"D:\SteamLibrary\steamapps\common\Example\Example.exe";
        Check("Steam game", "Example", "Example", steamPath, true, true);
        Check("case insensitive paths", "Example", "Example", "d:/steamlibrary/STEAMAPPS/COMMON/Example/game.exe", true, true);
        Check("Epic game", "Example", "Example", @"D:\Epic Games\Example\Example.exe", true, true);
        Check("GOG game", "Example", "Example", @"D:\GOG Games\Example\Example.exe", true, true);
        Check("Xbox game", "Example", "Example", @"D:\XboxGames\Example\Content\Example.exe", true, true);
        Check("Unreal game", "Example-Win64-Shipping", "Example", null, true, true);
        Check("Unity standalone", "Example", "Example", @"D:\Games\Example.exe", true, true, "UnityPlayer.dll");
        Check("Godot standalone", "Example", "Example", null, true, true, "libgodot.windows.opt.64.dll");
        Check("Minecraft Java", "javaw", "Minecraft 1.21", null, true, true);
        Check("ordinary Java app", "javaw", "IDE", null, true, false);
        Check("Minecraft name retains dots", "Minecraft.Windows", "Minecraft", null, true, true);
        Check("Roblox", "RobloxPlayerBeta.exe", "Roblox", null, true, true);
        Check("background game", "Example", "Example", steamPath, false, false);
        Check("untitled game helper", "Example", " ", steamPath, true, false);
        Check("ordinary app", "notepad", "Notes", @"C:\Apps\notepad.exe", true, false);
        Check("graphics API alone is insufficient", "Example", "Editor", null, true, false, "d3d11.dll", "dxgi.dll");
        Check("a title alone is insufficient", "notepad", "Minecraft notes", null, true, false);
        Check("no executable access or game signal", "Example", "Example", null, true, false);
        Check("path segment boundary", "Example", "Example", @"D:\Notsteamapps\common\Example.exe", true, false);

        foreach (var name in new[] { "Spotify", "CHROME", "chrome.exe", "Code", "Code - Insiders", "Battle.net",
                     "steam", "EpicGamesLauncher", "RiotClientUx", "Discord", "wallpaper64", "blender", "setup" })
        {
            Check($"excluded {name} overrides game signals", name, "Game", steamPath, true, false, "UnityPlayer.dll");
        }

        foreach (var path in new[] {
                     @"C:\Users\User\AppData\Roaming\Spotify\helper.exe",
                     @"C:\Program Files\Google\Chrome\Application\helper.exe",
                     @"C:\Users\User\AppData\Local\Programs\Microsoft VS Code\helper.exe",
                     @"D:\Epic Games\Launcher\Portal\Binaries\helper.exe",
                     @"D:\Riot Games\Riot Client\helper.exe",
                     @"C:\Windows\System32\helper.exe",
                     @"D:\steamapps\common\Steamworks Shared\setup.exe" })
        {
            Check($"excluded directory {path}", "helper", "Game", path, true, false, "UnityPlayer.dll");
        }

        Console.WriteLine($"PASS: {checks} game discovery cases.");
    }

    private static void CheckLiveDiscovery()
    {
        var service = new ProcessService();
        var games = service.GetProcesses();
        Assert(games.All(game => !GameProcessFilter.IsExcluded(game.Name)), "live list excludes blocked apps");
        Assert(games.All(game => !string.IsNullOrWhiteSpace(game.Title)), "live list has titled windows");
        foreach (var name in new[] { "Spotify", "chrome.exe", "Code", "Code - Insiders", "Battle.net" })
            Assert(service.FindProcessByName(name) is null, $"auto attach excludes {name}");
        Console.WriteLine($"PASS: live discovery returned {games.Count} game(s).");
    }

    private static void CheckLayout(bool preview)
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        window.Show();
        var refresh = (Button)window.FindName("RefreshGamesButton");
        var timeout = Stopwatch.StartNew();
        while (!refresh.IsEnabled && timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            DrainDispatcher();
            Thread.Sleep(10);
        }
        Assert(refresh.IsEnabled, "async discovery finishes");
        var root = (FrameworkElement)window.Content;
        var picker = (ComboBox)window.FindName("GameComboBox");
        var attach = (Button)window.FindName("AttachButton");
        var prompt = (TextBlock)window.FindName("NoGamesText");
        Assert(!attach.IsEnabled, "attach disabled without a selection");

        picker.ItemsSource = new[] {
            new ProcessItem { Id = 1234, Name = "Example-Win64-Shipping", Title = "Example Game" },
            new ProcessItem { Id = 5678, Name = "AnotherGame", Title = "Another Game" }
        };
        picker.SelectedIndex = 0;
        ((TextBlock)window.FindName("ProcessCountText")).Text = "2";
        Assert(attach.IsEnabled && prompt.Visibility == Visibility.Collapsed, "selecting a game enables attach");

        foreach (var (width, height) in new[] { (1180, 850), (960, 720) })
        {
            window.Width = width;
            window.Height = height;
            DrainDispatcher();
            root.UpdateLayout();
            var firstScan = (Button)window.FindName("FirstScanButton");
            var nextScan = (Button)window.FindName("NextScanButton");
            Assert(firstScan.ActualWidth > 90 && nextScan.ActualWidth > 90, "scan controls fit sidebar");
            Assert(picker.ActualWidth > 200, "game picker fits sidebar");
            var results = (DataGrid)window.FindName("ResultsGrid");
            Console.WriteLine($"Layout {width}x{height}: results {results.ActualWidth:0}x{results.ActualHeight:0}.");
            Assert(results.Columns.All(column => column.ActualWidth >= 60), "all result columns remain visible");
            var scanBottom = firstScan.TranslatePoint(new Point(0, firstScan.ActualHeight), root).Y;
            var resultsBottom = results.TranslatePoint(new Point(0, results.ActualHeight), root).Y;
            Assert(scanBottom <= resultsBottom + 44, "primary scan action stays inside the sidebar");

            if (preview)
            {
                var output = Path.Combine(Directory.GetCurrentDirectory(), "Tests", "Previews");
                Directory.CreateDirectory(output);
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"main-{width}x{height}.png"));
                encoder.Save(stream);
            }
            Assert(results.ActualWidth > 500 && results.ActualHeight > 180, "results retain usable space");
        }

        picker.SelectedIndex = -1;
        Assert(!attach.IsEnabled && prompt.Visibility == Visibility.Visible, "clearing selection resets attach");
        CheckImportUi(window);
        window.Close();
        app.Shutdown();
    }

    private static void CheckImportUi(MainWindow window)
    {
        var menu = (MenuItem)window.FindName("ImportCheatTableMenuItem");
        Assert(menu.IsEnabled && menu.Header.ToString()!.Contains(".CT"), "CT import action is available");
        for (var count = 1; count <= 2; count++)
        {
            var import = window.Dispatcher.InvokeAsync(
                () => window.ImportCheatTableAsync(CheatTableImportChecks.FixturePath)).Task.Unwrap();
            var timeout = Stopwatch.StartNew();
            while (!import.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                DrainDispatcher();
                Thread.Sleep(10);
            }
            if (import.IsFaulted)
                import.GetAwaiter().GetResult();
            Assert(import.IsCompletedSuccessfully, $"UI import completes ({import.Status})");
            var grid = (DataGrid)window.FindName("AddressGrid");
            Assert(grid.Items.Count == import.Result.Entries.Count * count, "import appends to existing addresses");
        }
        var project = window.BuildProjectFile();
        Assert(project.ProjectName == "game" && project.ProcessName == "game", "new imported project metadata");
        Assert(project.Addresses[0].AddressExpression == "game.exe|0x1234|-0x20,0x10", "UI save retains pointer definition");
        Assert(menu.IsEnabled, "import action re-enables after completion");
        Console.WriteLine("PASS: WPF import action, append behavior, and project metadata.");
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"FAIL: {message}");
    }
}
