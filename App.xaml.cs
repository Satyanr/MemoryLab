using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MemoryLab;

public partial class App : Application
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            ApplyDarkTitleBar(window);
        }
    }

    private static void ApplyDarkTitleBar(Window window)
    {
        try
        {
            var hwnd =
                new WindowInteropHelper(window).Handle;

            if (hwnd == IntPtr.Zero)
                return;

            var enabled = 1;

            var result =
                DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref enabled,
                    sizeof(int));

            if (result != 0)
            {
                DwmSetWindowAttribute(
                    hwnd,
                    DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY,
                    ref enabled,
                    sizeof(int));
            }
        }
        catch
        {
            // Windows lama / DWM unavailable:
            // biarkan title bar default.
        }
    }
}