$ErrorActionPreference = 'SilentlyContinue'

$taskName = 'HOI4 Mod Overlay Watcher'
Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

Write-Output "REMOVED=$taskName"
