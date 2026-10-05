@echo off
setlocal
dotnet restore --configfile NuGet.Config
if errorlevel 1 exit /b %errorlevel%
dotnet build -c Release --no-restore
exit /b %errorlevel%
