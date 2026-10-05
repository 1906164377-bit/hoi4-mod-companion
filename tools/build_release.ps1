$ErrorActionPreference = 'Stop'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnet restore --configfile NuGet.Config
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet build -c Release --no-restore
exit $LASTEXITCODE
