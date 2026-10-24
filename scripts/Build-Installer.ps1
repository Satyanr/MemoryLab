[CmdletBinding()]
param([switch] $SkipTests, [string] $CompilerPath)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $repositoryRoot
try {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Build-BrandAssets.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Brand asset generation failed.' }

    if (!$SkipTests) {
        & dotnet run --project Tests\MemoryLab.Tests.csproj -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw 'Application checks failed.' }
    }
    & dotnet build MemoryLab.csproj --configuration Release -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    & dotnet publish MemoryLab.csproj --configuration Release --runtime win-x64 --self-contained true -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

    if (!$CompilerPath) { $CompilerPath = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1') }
    if (!(Test-Path -LiteralPath $CompilerPath)) { throw "Compiler not found: $CompilerPath" }
    [xml] $project = Get-Content -LiteralPath 'MemoryLab.csproj'
    $version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "Invalid application version: $version" }
    & $CompilerPath "/DAppVersion=$version" 'Installer\MemoryLab.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

    $installer = Join-Path $repositoryRoot "Installer\MemoryLab-Setup-$version-x64.exe"
    if (!(Test-Path -LiteralPath $installer)) { throw 'Compiler did not produce an installer.' }
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath "$installer.sha256" -Encoding ASCII
    Write-Host "Installer ready: $installer"
}
finally { Pop-Location }
