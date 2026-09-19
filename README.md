# MemoryLab — Phase 1

MemoryLab adalah starter project WPF .NET 8 untuk mempelajari process inspection dan memory reading pada Windows.

## Phase 1 features

- Menampilkan process yang sedang berjalan.
- Refresh process list.
- Pilih dan attach ke process.
- Menampilkan PID, main module, dan base address jika dapat diakses.
- Manual memory read:
  - Int32
  - Float
  - 16 raw bytes
- Wrapper aman untuk `OpenProcess`, `ReadProcessMemory`, dan `CloseHandle`.

## Requirements

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022 atau VS Code + C# Dev Kit

## Run

Dari folder project:

```powershell
dotnet restore
dotnet build
dotnet run
```

Atau buka `MemoryLab.csproj` di Visual Studio.

## Permissions

Beberapa process Windows memiliki privilege lebih tinggi. Jika process target milik aplikasi pengujianmu sendiri berjalan sebagai Administrator, MemoryLab juga perlu dijalankan sebagai Administrator agar bisa membacanya.

## Testing

Gunakan aplikasi milikmu sendiri / aplikasi test sederhana. Jangan menggunakan tool ini untuk bypass anti-cheat, anti-tamper, DRM, atau proteksi sistem pihak ketiga.

## Next

Phase 2:
- VirtualQueryEx
- enumerasi readable memory region
- First Scan
- Exact Value
- Int32 / Float
- hasil address
- Next Scan
