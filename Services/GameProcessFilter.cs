namespace MemoryLab.Services;

// Conservative discovery: a visible window alone does not make an app a game.
internal static class GameProcessFilter
{
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "MemoryLab", "Spotify", "chrome", "chrome_proxy", "Code", "Code - Insiders",
        "CodeHelper", "msedge", "msedgewebview2", "firefox", "brave", "opera",
        "explorer", "ApplicationFrameHost", "SystemSettings", "Taskmgr",
        "steam", "steamwebhelper", "GameOverlayUI", "EpicGamesLauncher", "EpicWebHelper",
        "GalaxyClient", "GalaxyClient Helper", "Battle.net", "Agent", "EADesktop", "EALauncher",
        "UbisoftConnect", "upc", "RiotClientServices", "RiotClientUx", "RiotClientUxRender",
        "Discord", "DiscordCanary", "obs64", "obs32", "Unity", "Unity Hub", "UnrealEditor",
        "devenv", "WindowsTerminal", "OpenConsole", "conhost", "cmd", "powershell", "pwsh",
        "wallpaper32", "wallpaper64", "wallpaper_engine", "wallpaperUI", "blender", "aseprite",
        "LosslessScaling", "Godot", "CrashReportClient", "UnityCrashHandler64", "UnityCrashHandler32",
        "setup", "DXSETUP", "vc_redist.x64", "vc_redist.x86"
    };

    private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RobloxPlayerBeta", "Minecraft.Windows", "GenshinImpact", "StarRail", "ZenlessZoneZero",
        "League of Legends", "VALORANT-Win64-Shipping", "osu!", "osu"
    };

    private static readonly string[] ExcludedDirectories =
    {
        @"\Spotify\", @"\Google\Chrome\", @"\Google\Chrome Beta\", @"\Google\Chrome SxS\",
        @"\Microsoft VS Code\", @"\Microsoft VS Code Insiders\", @"\Visual Studio Code\",
        @"\Windows\", @"\Epic Games\Launcher\", @"\Riot Games\Riot Client\",
        @"\steamapps\common\Steamworks Shared\"
    };

    private static readonly string[] GameDirectories =
    {
        @"\steamapps\common\", @"\Epic Games\", @"\GOG Games\", @"\GOG Galaxy\Games\",
        @"\XboxGames\", @"\Riot Games\", @"\Battle.net\Games\", @"\EA Games\",
        @"\Origin Games\", @"\Ubisoft Game Launcher\games\"
    };

    internal static bool IsExcluded(string name, string? executablePath = null)
    {
        if (ExcludedNames.Contains(NormalizeName(name)))
            return true;

        var path = NormalizePath(executablePath);
        return ExcludedDirectories.Any(directory =>
            path.Contains(directory, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsGame(
        string name,
        string title,
        string? executablePath,
        bool hasVisibleWindow,
        IEnumerable<string>? moduleNames = null)
    {
        if (!hasVisibleWindow || string.IsNullOrWhiteSpace(title) || IsExcluded(name, executablePath))
            return false;

        name = NormalizeName(name);
        var path = NormalizePath(executablePath);

        if (KnownGames.Contains(name) ||
            name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("-Win32-Shipping", StringComparison.OrdinalIgnoreCase) ||
            GameDirectories.Any(directory => path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Java itself is not a game. Only recognize the actual Minecraft window.
        if ((name.Equals("javaw", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("java", StringComparison.OrdinalIgnoreCase)) &&
            (title.Equals("Minecraft", StringComparison.OrdinalIgnoreCase) ||
             title.StartsWith("Minecraft ", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return moduleNames?.Any(module =>
            module.Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase) ||
            module.Equals("libgodot.windows.opt.64.dll", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static string NormalizePath(string? path) => (path ?? string.Empty).Replace('/', '\\');

    private static string NormalizeName(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
}
