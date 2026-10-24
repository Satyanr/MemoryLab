[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
try {
    $repositoryRoot = Split-Path $PSScriptRoot -Parent
    [xml] $project = Get-Content -LiteralPath (Join-Path $repositoryRoot 'MemoryLab.csproj')
    $version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Invalid application version.' }
    $installer = Join-Path $repositoryRoot "Installer\MemoryLab-Setup-$version-x64.exe"
    if (!(Test-Path -LiteralPath $installer)) {
        throw 'Bundled installer not found. Ask the repository maintainer to include Installer/MemoryLab-Setup-*.exe, or run Build-Installer.cmd with the .NET 8 SDK installed.'
    }
    # This is the interactive wizard explicitly launched by the user.
    $setup = Start-Process -FilePath $installer -WindowStyle Normal -Wait -PassThru
    exit $setup.ExitCode
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
