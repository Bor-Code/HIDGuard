param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$rootPath = Resolve-Path "$PSScriptRoot/.."
Set-Location $rootPath

$stagingPath = Join-Path $rootPath "dist/package-staging"
$resolvedOutputDir = Join-Path $rootPath $OutputDir

if (Test-Path $stagingPath) {
    Remove-Item -Recurse -Force $stagingPath
}

if (!(Test-Path $resolvedOutputDir)) {
    New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null
}

New-Item -ItemType Directory -Force -Path "$stagingPath/app" | Out-Null
New-Item -ItemType Directory -Force -Path "$stagingPath/simulator" | Out-Null

Write-Host "Restoring and building solution in $Configuration configuration..."
dotnet restore HidGuard.sln
dotnet build HidGuard.sln --configuration $Configuration --no-restore

Write-Host "Executing test suites..."
dotnet test HidGuard.sln --configuration $Configuration --no-build

Write-Host "Running automated end-to-end attack simulation verification..."
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj --configuration $Configuration --no-build -- --verify

$appPublishArgs = @(
    "--configuration", $Configuration,
    "--output", "$stagingPath/app"
)

if ($SelfContained) {
    $appPublishArgs += @("--runtime", "win-x64", "--self-contained", "true")
}

Write-Host "Publishing application binaries..."
dotnet publish src/HidGuard.App/HidGuard.App.csproj @appPublishArgs

$simPublishArgs = @(
    "--configuration", $Configuration,
    "--output", "$stagingPath/simulator"
)

if ($SelfContained) {
    $simPublishArgs += @("--runtime", "win-x64", "--self-contained", "true")
}

Write-Host "Publishing simulator binaries..."
dotnet publish tools/HidGuard.Simulator/HidGuard.Simulator.csproj @simPublishArgs

Copy-Item -Path "$rootPath/README.md" -Destination "$stagingPath/README.md"
Copy-Item -Path "$rootPath/README.tr.md" -Destination "$stagingPath/README.tr.md"
Copy-Item -Path "$rootPath/LICENSE" -Destination "$stagingPath/LICENSE"
Copy-Item -Path "$rootPath/SECURITY.md" -Destination "$stagingPath/SECURITY.md"

$zipName = "HIDGuard-v$Version-win-x64.zip"
$zipPath = Join-Path $resolvedOutputDir $zipName

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Compressing release package to $zipPath..."
Compress-Archive -Path "$stagingPath/*" -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash
$checksumPath = Join-Path $resolvedOutputDir "SHA256SUMS.txt"
"$hash  $zipName" | Out-File -FilePath $checksumPath -Encoding utf8

Remove-Item -Recurse -Force $stagingPath

Write-Host "Release packaging completed successfully."
Write-Host "Package: $zipPath"
Write-Host "SHA256:  $hash"
