<#
.SYNOPSIS
  Builds KeyShot and installs it for the current user (no admin needed).

.DESCRIPTION
  - Publishes a single-file KeyShot.exe (+ Sounds folder) to %LOCALAPPDATA%\Programs\KeyShot
  - Creates "KeyShot" shortcuts on the Desktop and in the Start Menu
  - Optionally starts KeyShot with Windows (-Autostart) and launches it (-Launch)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\install.ps1 -Launch
#>
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\KeyShot'),
    [switch]$Launch,
    [switch]$Autostart,
    [switch]$NoShortcuts
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

# Locate a .NET SDK: PATH first, then the per-user install location used by dotnet-install.ps1.
$dotnet = $null
foreach ($candidate in @((Get-Command dotnet -ErrorAction SilentlyContinue).Source, (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'))) {
    if ($candidate -and (Test-Path $candidate) -and (& $candidate --list-sdks 2>$null | Select-String '^10\.')) { $dotnet = $candidate; break }
}
if (-not $dotnet) { throw '.NET 10 SDK not found. Install it from https://dot.net/download and re-run.' }
Write-Host "Using $dotnet"

# Stop a running copy so its files can be replaced.
Get-Process KeyShot -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

& $dotnet publish (Join-Path $root 'src\KeyShot.App\KeyShot.App.csproj') `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=embedded `
    -o $InstallDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }

$exe = Join-Path $InstallDir 'KeyShot.exe'
Write-Host "Installed to $exe" -ForegroundColor Green

function New-Shortcut([string]$path, [string]$arguments = '') {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($path)
    $lnk.TargetPath = $exe
    $lnk.Arguments = $arguments
    $lnk.WorkingDirectory = $InstallDir
    $lnk.IconLocation = "$exe,0"
    $lnk.Description = 'KeyShot - shotgun sounds for every keystroke'
    $lnk.Save()
    Write-Host "Shortcut: $path"
}

if (-not $NoShortcuts) {
    New-Shortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) 'KeyShot.lnk')
    New-Shortcut (Join-Path ([Environment]::GetFolderPath('Programs')) 'KeyShot.lnk')
}
if ($Autostart) {
    New-Shortcut (Join-Path ([Environment]::GetFolderPath('Startup')) 'KeyShot.lnk') '--minimized'
}
if ($Launch) { Start-Process $exe }
