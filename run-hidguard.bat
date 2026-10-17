@echo off
if exist "publish\HidGuard.App.exe" (
    start "" "publish\HidGuard.App.exe"
) else (
    dotnet run --project src\HidGuard.App\HidGuard.App.csproj --configuration Release
)
