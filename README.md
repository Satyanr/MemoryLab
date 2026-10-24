# MemoryLab v1.0 — Phase 8 Final

Phase 8 adalah build kumulatif final dari roadmap awal MemoryLab.

<p><img src="Assets/Brand/MemoryLab.png" width="88" height="88" alt="MemoryLab logo: memory chip with an M" /></p>

## Install untuk pengguna Windows

1. Download ZIP repo ini, lalu **Extract All** terlebih dahulu.
2. Klik dua kali **`Install-MemoryLab.cmd`** di folder utama, atau buka
   [MemoryLab-Setup-1.0.0-x64.exe](Installer/MemoryLab-Setup-1.0.0-x64.exe) di folder `Installer`.
3. Ikuti wizard dan biarkan **Create a desktop shortcut** tercentang.
4. Setelah selesai, buka **MemoryLab** dari shortcut desktop atau Start Menu.

Installer menyertakan runtime .NET; pengguna tidak perlu menginstal .NET atau menjalankan source code.
Target: Windows 10/11 x64. Instalasi dilakukan untuk pengguna saat ini di
`%LOCALAPPDATA%\Programs\MemoryLab`, tanpa meminta hak administrator.
Uninstall tersedia melalui Windows **Settings > Apps > Installed apps > MemoryLab > Uninstall**.
Project dan pengaturan pengguna tidak dihapus oleh uninstaller.

**Untuk pemilik repo:** sertakan file `Installer/MemoryLab-Setup-1.0.0-x64.exe` dan `.sha256`
saat commit/push agar ikut tersedia di ZIP repo. Installer di workspace belum otomatis terunggah ke GitHub.
File installer juga dapat dibagikan sendiri; pengguna tidak perlu mengunduh seluruh source.

## Membuat ulang installer

Prasyarat untuk pembuat installer: Windows, .NET 8 SDK, dan internet saat menyiapkan compiler pertama kali.
Jalankan di folder repo:

```powershell
.\Build-Installer.cmd
```

Script akan membuat ulang logo/ikon, menjalankan tes aplikasi, memperbarui build Release dan publish,
lalu membuat `Installer/MemoryLab-Setup-<Version>-x64.exe` beserta checksum SHA-256.
Versi diambil dari `MemoryLab.csproj`. Compiler [Inno Setup](https://jrsoftware.org/isinfo.php)
diunduh dari rilis resmi dengan versi dan SHA-256 yang dipatok, lalu disiapkan secara portable di `.tools`.
Hasil build lokal belum ditandatangani secara digital.

Untuk menguji install, reinstall, shortcut, dan uninstall di folder terisolasi:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-Installer.ps1
```

Pengujian ini memakai `Installer/TestOutput`, tanpa membuat shortcut desktop atau entri uninstall sungguhan.
Source logo vektor berada di `Assets/Brand/LogoDrawing.xaml`; PNG, ICO multi-resolusi, dan gambar wizard
dibuat ulang oleh `scripts/Build-BrandAssets.ps1`. Ikon aksi berada di `Assets/Icons/Icons.xaml`.


## Core features

### Process & memory
- Focused running-game picker
- Attach process
- OpenProcess
- ReadProcessMemory
- WriteProcessMemory
- VirtualQueryEx
- readable memory-region enumeration

### Scanner
- Int32
- Float
- Exact Value
- Unknown Initial Value
- Changed / Unchanged
- Increased / Decreased
- Greater Than / Less Than
- Between
- Increased By / Decreased By
- First Scan / Next Scan
- Fast Scan 4-byte alignment
- configurable result cap
- cancel + progress
- bulk UI result replacement

### Address List
- description
- groups
- live refresh
- edit value
- freeze / unfreeze
- remove
- local hotkeys

### Memory Viewer
- hexadecimal viewer
- ASCII preview
- jump to address
- previous / next page
- region base / size / state / type / protection
- configurable live refresh

### Pointer tools
- module list
- module + offset representation
- 64-bit pointer dereference
- manual pointer-chain resolver
- one-level pointer scan
- save resolved pointer into Address List

### Project system
- save/load `*.mlab.json`
- import Cheat Engine XML tables (`*.CT`)
- project name
- target process name
- Address List persistence
- Group persistence
- recent projects
- auto attach
- module+offset auto re-resolution
- pointer-chain auto re-resolution

## New in Phase 8

### Import Cheat Engine tables

Use `Project > Import Cheat Engine table (.CT)...` and choose a file such as `game.CT`.
`Open project` (Ctrl+O) also accepts `.CT` files. Import appends addresses to the current list;
use `New project` first when you want a separate project. Attach to the game to read live values,
then use `Save` to store the imported list as a MemoryLab `.mlab.json` project.

Supported entries include Byte, 2/4/8 Bytes (signed or unsigned), Float, Double, absolute hex
addresses, module-relative addresses, and pointer chains with signed hex offsets. Descriptions
and nested groups are preserved. Pointer offsets follow Cheat Engine's reverse XML order,
and pointers are resolved again during live refresh and before editing/freezing a value.
Pointer reads use the attached target's 32-bit or 64-bit width. Numeric values display in decimal.

The import summary reports skipped entries. Lua/Auto Assembler scripts, script-defined symbols,
relative child addresses, String, Array of byte, Binary/custom types, hotkeys, dropdown labels,
and compressed tables are not imported. Table scripts are not executed; entries start unfrozen.
Saved `LastState` values and resolved addresses are ignored. Supported children of unsupported
entries are still imported when they have independent addresses. An unambiguous `.exe` module
can fill an empty project target; the importer does not infer a process from the table filename.

The XML and pointer ordering are based on
[Cheat Engine's memory record implementation](https://github.com/cheat-engine/cheat-engine/blob/master/Cheat%20Engine/MemoryRecordUnit.pas).

### Recent Projects UI

Use `Project > Recent projects` to open one of the last 10 projects.

Paths are stored at:

```text
%LOCALAPPDATA%\MemoryLab\recent-projects.json
```

### Application Settings

Use `Tools > Settings` to configure:

```text
Address List refresh: 100–5000 ms
Memory Viewer refresh: 100–5000 ms
Scan result limit: 10,000–1,000,000
```

Settings are stored at:

```text
%LOCALAPPDATA%\MemoryLab\settings.json
```

### Module+Offset persistence

Select a saved address and choose `More > Make module relative`:

```text
Make Module Relative
```

Example:

```text
MemoryLab.TestTarget.exe+0x1234
```

When the project is loaded and the process attaches again,
MemoryLab resolves the current module base automatically.

### Pointer-chain persistence

After resolving a chain in Pointer Tools, click:

```text
Save Resolved Pointer to Address List
```

The chain is stored as metadata, not only as the final dynamic address.

When the target process restarts, MemoryLab can resolve it again.

### Process restart handling

MemoryLab checks the attached process periodically.

If it exits:

```text
<process exited>
```

is shown in saved Address List entries.

Restart the target and use:

```text
F5
```

or:

```text
Auto Attach
```

to reconnect.

### Performance polish

Scan results now use bulk collection replacement instead of adding
potentially hundreds of thousands of rows to WPF one-by-one.

DataGrid row virtualization remains enabled.

### Version / About

MemoryLab now reports:

```text
Version 1.0.0
```

## Hotkeys

Hotkeys are local to the MemoryLab window:

```text
Ctrl+S  Save Project
Ctrl+O  Open Project
Ctrl+N  New Project
F5      Auto Attach
F6      Toggle Freeze selected Address List entry
```

No global keyboard hook is installed.

## Running-game selection

The main picker only shows detected games with visible, titled windows in the current Windows session.
Spotify, Chrome, VS Code, launchers, common desktop tools, and background processes are excluded.
Detection uses common game installation folders, known game executables, and Unity/Godot engine modules;
graphics libraries alone do not qualify an app as a game. Some standalone games may not be recognized.

Use `Refresh` beside `Running games` after opening a game. A single detected game is selected automatically.
Advanced actions are under `Project`, `Tools`, or `More`; fast-scan alignment is under `Tools`.
Explicit project targets still support owned test apps via `Project > Project settings` and F5,
while Spotify, Chrome, and VS Code remain excluded from auto attach as well.

## Development build

```powershell
dotnet restore
dotnet build
dotnet run
```

Run filter, CT import, numeric memory, live-discovery, and layout checks (no extra test packages):

```powershell
dotnet run --project Tests/MemoryLab.Tests.csproj
```

Add `-- --preview` to save WPF previews at normal and minimum window sizes under `Tests/Previews`.

## Portable Windows x64 publish

A publish profile is included:

```text
Properties/PublishProfiles/PortableWinX64.pubxml
```

Build it with:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Output:

```text
bin\Release\net8.0-windows\win-x64\publish\
```

Because the release is self-contained, the target Windows machine does
not need to have the .NET 8 runtime installed separately.

## Suggested test flow

1. Run `TestTarget`.
2. Start MemoryLab, then open `Project > Project settings`.
3. Set the target process to `MemoryLab.TestTarget`, then use `Project > Auto attach` (F5).
4. Scan `Health = 100`.
5. Change Health from the TestTarget console.
6. Narrow with Next Scan.
7. Add the result to Address List.
8. Test Edit and Freeze.
9. Open Memory Viewer.
10. Test Pointer Tools.
11. Save the project.
12. Restart TestTarget.
13. Auto Attach and verify saved module/pointer entries re-resolve.

## Scope

MemoryLab is intended for:
- software you own,
- test targets,
- debugging,
- reverse-engineering education in controlled environments.

It intentionally does not contain:
- anti-cheat bypass,
- anti-tamper bypass,
- DRM bypass,
- kernel stealth drivers,
- process concealment,
- anti-detection techniques.

## Roadmap status

```text
Phase 1  Process & Memory Access            ✅
Phase 2  Memory Scanner                     ✅
Phase 3  Memory Editor & Freeze             ✅
Phase 4  Advanced Scan                      ✅
Phase 5  Memory Viewer                      ✅
Phase 6  Pointer & Address Tools            ✅
Phase 7  Table & Project System             ✅
Phase 8  Polishing & Optimization           ✅
```

MemoryLab v1.0 roadmap complete.
