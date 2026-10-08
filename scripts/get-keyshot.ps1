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

$repo = 'prabindersinghh/keyshot-typing'
$assetName = 'KeyShot-win-x64.zip'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\KeyShot'
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("keyshot-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp | Out-Null

try {
    Write-Host 'Finding the latest KeyShot release...' -ForegroundColor Cyan
    $release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'keyshot-installer' }
    $zipAsset = $release.assets | Where-Object name -eq $assetName
    $sumAsset = $release.assets | Where-Object name -eq 'SHA256SUMS.txt'
    if (-not $zipAsset) { throw "Release $($release.tag_name) has no $assetName." }

    Write-Host "Downloading KeyShot $($release.tag_name) ($([math]::Round($zipAsset.size / 1MB)) MB)..." -ForegroundColor Cyan
    $zip = Join-Path $tmp $assetName
    Invoke-WebRequest $zipAsset.browser_download_url -OutFile $zip -UseBasicParsing

    if ($sumAsset) {
        $sums = (Invoke-WebRequest $sumAsset.browser_download_url -UseBasicParsing).Content
        $expected = ($sums -split "`n" | Where-Object { $_ -match [regex]::Escape($assetName) + '\s*$' } | ForEach-Object { ($_ -split '\s+')[0] }) | Select-Object -First 1
        $actual = (Get-FileHash $zip -Algorithm SHA256).Hash
        if (-not $expected -or $actual -ne $expected.ToUpperInvariant()) { throw 'Checksum mismatch: the download is corrupt. Please try again.' }
        Write-Host 'Checksum OK.' -ForegroundColor DarkGray
    }

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

    Write-Host "KeyShot $($release.tag_name) installed. Starting it now..." -ForegroundColor Green
    Write-Host 'Press ENABLE (or Ctrl+Shift+K anywhere) and start typing. Pick sounds under Mode.'
    Start-Process $exe
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
