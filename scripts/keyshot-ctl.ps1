<#
.SYNOPSIS
  Sends a command to a running KeyShot over its control pipe.
.EXAMPLE
  ./keyshot-ctl.ps1 STATUS      # ACTIVE / INACTIVE
  ./keyshot-ctl.ps1 TOGGLE
  ./keyshot-ctl.ps1 STATS       # diagnostics: keys seen, shots fired, voices, device
#>
param([ValidateSet('STATUS', 'ENABLE', 'DISABLE', 'TOGGLE', 'SHOW', 'PING', 'STATS')][string]$Command = 'STATUS')

$pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'KeyShot.Control', [System.IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect(1500)
    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    $writer.WriteLine($Command)
    $reader = New-Object System.IO.StreamReader($pipe)
    $reader.ReadLine()
}
catch [System.TimeoutException] {
    Write-Output 'NOT RUNNING'
    exit 1
}
finally {
    $pipe.Dispose()
}
