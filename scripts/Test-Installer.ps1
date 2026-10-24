[CmdletBinding()]
param([string] $CompilerPath)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$testBase = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'Installer\TestOutput'))
$testRoot = [IO.Path]::GetFullPath((Join-Path $testBase ([Guid]::NewGuid().ToString('N'))))
if (!$testRoot.StartsWith($testBase + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installer test path is outside the intended test folder.'
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
if (!$CompilerPath) { $CompilerPath = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1') }
$publishRoot = Join-Path $repositoryRoot 'bin\Release\net8.0-windows\win-x64\publish'
$applicationRoot = Join-Path $testRoot 'App'
[xml] $project = Get-Content -LiteralPath (Join-Path $repositoryRoot 'MemoryLab.csproj')
$version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText

# The test variant redirects BOTH shortcuts into TestOutput and never creates an uninstall registry key.
& $CompilerPath '/Q' '/DInstallerTest=1' "/DTestRoot=$testRoot" "/DAppVersion=$version" "/O$testRoot" (Join-Path $repositoryRoot 'Installer\MemoryLab.iss')
if ($LASTEXITCODE -ne 0) { throw 'Test installer compilation failed.' }
$installer = Join-Path $testRoot "MemoryLab-Setup-$version-x64.exe"
$installArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
    "/DIR=`"$applicationRoot`"", "/LOG=`"$(Join-Path $testRoot 'install.log')`"")
$install = Start-Process -FilePath $installer -ArgumentList $installArguments -WindowStyle Hidden -Wait -PassThru
if ($install.ExitCode -ne 0) { throw "Installer exited with $($install.ExitCode). See $testRoot\install.log." }

foreach ($filename in @('MemoryLab.exe', 'MemoryLab.dll', 'coreclr.dll', 'PresentationFramework.dll')) {
    $installedFile = Join-Path $applicationRoot $filename
    if (!(Test-Path -LiteralPath $installedFile)) { throw "Missing installed file: $filename" }
    if ((Get-FileHash -LiteralPath $installedFile).Hash -ne (Get-FileHash -LiteralPath (Join-Path $publishRoot $filename)).Hash) {
        throw "Installed file differs from the published build: $filename"
    }
}

$shell = New-Object -ComObject WScript.Shell
try {
    foreach ($shortcutName in @('Desktop\MemoryLab.lnk', 'StartMenu\MemoryLab.lnk')) {
        $shortcutPath = Join-Path $testRoot $shortcutName
        if (!(Test-Path -LiteralPath $shortcutPath)) { throw "Missing shortcut: $shortcutName" }
        $shortcut = $shell.CreateShortcut($shortcutPath)
        try {
            if ($shortcut.TargetPath -ne (Join-Path $applicationRoot 'MemoryLab.exe')) { throw "Wrong shortcut target: $shortcutName" }
            if ($shortcut.WorkingDirectory -ne $applicationRoot) { throw "Wrong shortcut working directory: $shortcutName" }
            if (!$shortcut.IconLocation.StartsWith((Join-Path $applicationRoot 'MemoryLab.exe'), [StringComparison]::OrdinalIgnoreCase)) {
                throw "Wrong shortcut icon: $shortcutName"
            }
        }
        finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
    }
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
Write-Host 'PASS: self-contained app installed, payload hashes match, desktop/Start Menu shortcuts point to the branded EXE.'

# Exercise an upgrade using the same installer and verify user files survive uninstall.
$userProject = Join-Path $applicationRoot 'user-project.mlab.json'
'{"ProjectName":"Keep this project"}' | Set-Content -LiteralPath $userProject -Encoding UTF8
$upgrade = Start-Process -FilePath $installer -ArgumentList $installArguments -WindowStyle Hidden -Wait -PassThru
if ($upgrade.ExitCode -ne 0 -or !(Test-Path -LiteralPath $userProject)) { throw 'Reinstall did not preserve the project.' }
$uninstaller = Join-Path $applicationRoot 'unins000.exe'
$uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
    "/LOG=`"$(Join-Path $testRoot 'uninstall.log')`"") -WindowStyle Hidden -Wait -PassThru
if ($uninstall.ExitCode -ne 0) { throw "Uninstaller failed with $($uninstall.ExitCode)." }
if (Test-Path -LiteralPath (Join-Path $applicationRoot 'MemoryLab.exe')) { throw 'Uninstall left the installed EXE.' }
foreach ($shortcutName in @('Desktop\MemoryLab.lnk', 'StartMenu\MemoryLab.lnk')) {
    if (Test-Path -LiteralPath (Join-Path $testRoot $shortcutName)) { throw "Uninstall left shortcut: $shortcutName" }
}
if (!(Test-Path -LiteralPath $userProject)) { throw 'Uninstall removed the user project.' }
Write-Host 'PASS: reinstall succeeds; uninstall removes program and shortcuts while preserving user-created projects.'
Write-Host "Verification logs: $testRoot"
