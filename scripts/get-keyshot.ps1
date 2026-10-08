<#
.SYNOPSIS
  One-line KeyShot installer. No admin, no .NET install, no build tools.

.DESCRIPTION
  Paste into PowerShell:
      irm https://raw.githubusercontent.com/prabindersinghh/keyshot-typing/main/scripts/get-keyshot.ps1 | iex

  Downloads the latest release, verifies its SHA-256 checksum, installs it to
  %LOCALAPPDATA%\Programs\KeyShot, adds Desktop + Start Menu shortcuts and starts KeyShot.
  Run it again any time to update. Uninstall: delete that folder and the two shortcuts.
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is much faster without the progress bar
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# "latest/download" links always point at the newest release and, unlike the GitHub API,
# have no per-IP rate limit (which shared/mobile IPs hit easily).
$base = 'https://github.com/prabindersinghh/keyshot-typing/releases/latest/download'
$assetName = 'KeyShot-win-x64.zip'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\KeyShot'
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("keyshot-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp | Out-Null

function Get-WithRetry([string]$url, [string]$outFile) {
    for ($attempt = 1; ; $attempt++) {
        try {
            if ($outFile) { Invoke-WebRequest $url -OutFile $outFile -UseBasicParsing; return }
            return (Invoke-WebRequest $url -UseBasicParsing).Content
        }
        catch {
            if ($attempt -ge 3) { throw }
            Write-Host "  Network hiccup, retrying ($attempt/3)..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

try {
    Write-Host 'Downloading the latest KeyShot (about 70 MB)...' -ForegroundColor Cyan
    $zip = Join-Path $tmp $assetName
    Get-WithRetry "$base/$assetName" $zip

    $sums = Get-WithRetry "$base/SHA256SUMS.txt"
    if ($sums -is [byte[]]) { $sums = [Text.Encoding]::UTF8.GetString($sums) }
    $expected = ($sums -split "`n" | Where-Object { $_ -match [regex]::Escape($assetName) + '\s*$' } | ForEach-Object { ($_ -split '\s+')[0] }) | Select-Object -First 1
    $actual = (Get-FileHash $zip -Algorithm SHA256).Hash
    if (-not $expected -or $actual -ne $expected.ToUpperInvariant()) { throw 'Checksum mismatch: the download is corrupt. Please try again.' }
    Write-Host 'Checksum OK.' -ForegroundColor DarkGray

    # Update in place: overwrite shipped files, keep settings (%APPDATA%\KeyShot) and any packs you added.
    Get-Process KeyShot -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400
    Expand-Archive $zip -DestinationPath $installDir -Force

    $exe = Join-Path $installDir 'KeyShot.exe'
    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in [Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs')) {
        $lnk = $shell.CreateShortcut((Join-Path $folder 'KeyShot.lnk'))
        $lnk.TargetPath = $exe
        $lnk.WorkingDirectory = $installDir
        $lnk.IconLocation = "$exe,0"
        $lnk.Description = 'KeyShot - gunshot sounds for every keystroke'
        $lnk.Save()
    }

    $version = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''   # drop "+<commit>" suffix
    Write-Host "KeyShot $version installed. Starting it now..." -ForegroundColor Green
    Write-Host 'Press ENABLE (or Ctrl+Shift+K anywhere) and start typing. Pick sounds under Mode.'
    Start-Process $exe
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
