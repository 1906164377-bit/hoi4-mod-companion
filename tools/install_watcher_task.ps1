$ErrorActionPreference = 'Stop'

$taskName = 'HOI4 Mod Overlay Watcher'
$watcher = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'watch_hoi4.ps1'))
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

$action = New-ScheduledTaskAction -Execute $powershell -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$watcher`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero)
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Starts the HOI4 mod overlay automatically whenever hoi4.exe is running.' -Force | Out-Null
Start-ScheduledTask -TaskName $taskName

Write-Output "INSTALLED=$taskName"
Write-Output "WATCHER=$watcher"
