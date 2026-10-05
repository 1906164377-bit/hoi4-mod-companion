$ErrorActionPreference = 'SilentlyContinue'

$mutex = New-Object System.Threading.Mutex($false, 'Local\Hoi4ModOverlayWatcher')
$ownsMutex = $false

try {
    $ownsMutex = $mutex.WaitOne(0, $false)
    if (-not $ownsMutex) {
        exit 0
    }

    $releaseExe = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Hoi4ModOverlay.exe'))
    $devExe = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dist\Hoi4ModOverlay.exe'))
    $overlayExe = if (Test-Path $releaseExe) { $releaseExe } else { $devExe }

    while ($true) {
        $gameRunning = @(Get-Process -Name 'hoi4' -ErrorAction SilentlyContinue).Count -gt 0
        if ($gameRunning -and (Test-Path $overlayExe)) {
            $overlayRunning = @(Get-Process -Name 'Hoi4ModOverlay' -ErrorAction SilentlyContinue).Count -gt 0
            if (-not $overlayRunning) {
                Start-Process -FilePath $overlayExe -WorkingDirectory (Split-Path $overlayExe -Parent)
            }
        }

        Start-Sleep -Milliseconds 750
    }
}
finally {
    if ($ownsMutex) {
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}
