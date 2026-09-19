# MemoryLab v1.0 — Phase 8 Final

Phase 8 adalah build kumulatif final dari roadmap awal MemoryLab.


## Core features

### Process & memory
- Process list
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
- project name
- target process name
- Address List persistence
- Group persistence
- recent projects
- auto attach
- module+offset auto re-resolution
- pointer-chain auto re-resolution

## New in Phase 8

### Recent Projects UI

Use the `Recent` button to open one of the last 10 projects.

Paths are stored at:

```text
%LOCALAPPDATA%\MemoryLab\recent-projects.json
```

### Application Settings

Use `Settings` to configure:

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

Select an Address List entry and click:

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

## Development build

```powershell
dotnet restore
dotnet build
dotnet run
```

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
2. Start MemoryLab.
3. Attach to `MemoryLab.TestTarget`.
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
