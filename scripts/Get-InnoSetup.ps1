[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$toolRoot = Join-Path $repositoryRoot '.tools\InnoSetup-6.7.3'
$compiler = Join-Path $toolRoot 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) { return $compiler }

$downloadRoot = Join-Path $repositoryRoot '.tools\downloads'
New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
$download = Join-Path $downloadRoot 'innosetup-6.7.3.exe'
$expectedHash = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
$uri = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe'

if (!(Test-Path -LiteralPath $download)) {
    Write-Host 'Downloading the pinned official Inno Setup compiler...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $uri -OutFile $download -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expectedHash) {
    throw "Inno Setup checksum mismatch. Remove '$download' and retry."
}

# Official portable mode: workspace files only, no compiler shortcuts or uninstall registration.
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/PORTABLE=1',
    '/NOICONS', '/TASKS=', "/DIR=`"$toolRoot`"", "/LOG=`"$(Join-Path $downloadRoot 'compiler-setup.log')`"")
$process = Start-Process -FilePath $download -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $compiler)) {
    throw "Portable compiler setup failed (exit $($process.ExitCode)). See .tools\downloads\compiler-setup.log."
}
return $compiler
