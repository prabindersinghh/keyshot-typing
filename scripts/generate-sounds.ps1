<#
.SYNOPSIS
  Synthesizes KeyShot's default shotgun samples (original audio, released as CC0).

.DESCRIPTION
  Each shot is three layers summed and saturated:
    crack  - mid-focused noise burst (150 Hz - 3.6 kHz closing to 1 kHz), ~30 ms decay
    boom   - sine sweeping ~150 Hz -> ~50 Hz, ~110 ms decay (the "thump")
    room   - band-limited noise (150 Hz - 1.2 kHz), ~360 ms decay, decorrelated per channel
  Output: src/KeyShot.App/Sounds/Shotgun/shotgun-<n>.wav  (48 kHz, 16-bit stereo)
  Deterministic: the same seeds always produce identical files.
#>
param([int]$SampleRate = 48000, [double]$Seconds = 1.3)
$ErrorActionPreference = 'Stop'
$outDir = Join-Path $PSScriptRoot '..\src\KeyShot.App\Sounds\Shotgun'
New-Item -ItemType Directory -Force $outDir | Out-Null

function New-Shot([int]$seed, [double]$boomHz, [double]$crackTau, [double]$tailTau) {
    $n = [int]($SampleRate * $Seconds)
    $sr = [double]$SampleRate
    $left = New-Object double[] $n
    $right = New-Object double[] $n
    $rngShared = New-Object System.Random $seed
    $rngL = New-Object System.Random ($seed * 7 + 1)
    $rngR = New-Object System.Random ($seed * 7 + 2)

    $crackLp = 0.0; $crackHp = 0.0
    $tL1 = 0.0; $tL2 = 0.0; $tLh = 0.0; $tR1 = 0.0; $tR2 = 0.0; $tRh = 0.0
    $phase = 0.0
    $twoPi = 2.0 * [Math]::PI
    $tailLp = 1.0 - [Math]::Exp(-$twoPi * 1200.0 / $sr)  # room tail: band-limited 150 Hz - 1.2 kHz
    $hp150 = 1.0 - [Math]::Exp(-$twoPi * 150.0 / $sr)

    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sr

        # Crack: mid-focused noise burst (the "snap" real recordings carry at 0.6-3 kHz).
        $noise = $rngShared.NextDouble() * 2.0 - 1.0
        $fc = 2600.0 * [Math]::Exp(-$t / 0.03) + 1000.0
        $a = 1.0 - [Math]::Exp(-$twoPi * $fc / $sr)
        $crackLp += $a * ($noise - $crackLp)
        $crackHp += $hp150 * ($crackLp - $crackHp)
        $crack = ($crackLp - $crackHp) * [Math]::Exp(-$t / $crackTau) * 2.4

        # Click: the first ~1 ms, unfiltered, gives the attack its edge.
        $click = $noise * [Math]::Exp(-$t / 0.0007) * 0.3

        # Boom: short falling sine (chest punch), kept well below the crack.
        $f = $boomHz * 0.35 + $boomHz * [Math]::Exp(-$t / 0.03)
        $phase += $f / $sr
        $boom = [Math]::Sin($twoPi * $phase) * (1.0 - [Math]::Exp(-$t / 0.001)) * [Math]::Exp(-$t / 0.11) * 0.8

        # Room tail: independent band-limited noise per channel (stereo width), slow decay.
        $env = [Math]::Exp(-$t / $tailTau) * (1.0 - [Math]::Exp(-$t / 0.015)) * 2.6
        $tL1 += $tailLp * (($rngL.NextDouble() * 2.0 - 1.0) - $tL1); $tL2 += $tailLp * ($tL1 - $tL2); $tLh += $hp150 * ($tL2 - $tLh)
        $tR1 += $tailLp * (($rngR.NextDouble() * 2.0 - 1.0) - $tR1); $tR2 += $tailLp * ($tR1 - $tR2); $tRh += $hp150 * ($tR2 - $tRh)

        $mid = $crack + $click + $boom
        $left[$i] = $mid + ($tL2 - $tLh) * $env
        $right[$i] = $mid + ($tR2 - $tRh) * $env
    }

    # Gentle saturation (glues the layers without crushing dynamics), normalize, fade the last 60 ms.
    $drive = 1.2; $norm = [Math]::Tanh($drive)
    $peak = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $left[$i] = [Math]::Tanh($drive * $left[$i]) / $norm
        $right[$i] = [Math]::Tanh($drive * $right[$i]) / $norm
        $peak = [Math]::Max($peak, [Math]::Max([Math]::Abs($left[$i]), [Math]::Abs($right[$i])))
    }
    $gain = 0.95 / $peak
    $fade = [int]($sr * 0.06)
    for ($i = 0; $i -lt $n; $i++) {
        $g = $gain
        if ($i -gt $n - $fade) { $g *= ($n - $i) / [double]$fade }
        $left[$i] *= $g; $right[$i] *= $g
    }
    return , @($left, $right)
}

function Write-Wav([string]$path, $channels) {
    $left = $channels[0]; $right = $channels[1]
    $n = $left.Length
    $dataBytes = $n * 4
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $bw.Write([int](36 + $dataBytes)); $bw.Write([Text.Encoding]::ASCII.GetBytes('WAVE'))
    $bw.Write([Text.Encoding]::ASCII.GetBytes('fmt ')); $bw.Write([int]16); $bw.Write([int16]1); $bw.Write([int16]2)
    $bw.Write([int]$SampleRate); $bw.Write([int]($SampleRate * 4)); $bw.Write([int16]4); $bw.Write([int16]16)
    $bw.Write([Text.Encoding]::ASCII.GetBytes('data')); $bw.Write([int]$dataBytes)
    for ($i = 0; $i -lt $n; $i++) {
        $bw.Write([int16][Math]::Round([Math]::Max(-1.0, [Math]::Min(1.0, $left[$i])) * 32767))
        $bw.Write([int16][Math]::Round([Math]::Max(-1.0, [Math]::Min(1.0, $right[$i])) * 32767))
    }
    $bw.Dispose()
    Write-Host "Wrote $path"
}

# Three variants so KeyShot's random sample selection has something to choose from.
Write-Wav (Join-Path $outDir 'shotgun-1.wav') (New-Shot 11 150 0.030 0.36)
Write-Wav (Join-Path $outDir 'shotgun-2.wav') (New-Shot 23 135 0.036 0.40)
Write-Wav (Join-Path $outDir 'shotgun-3.wav') (New-Shot 37 165 0.026 0.32)
