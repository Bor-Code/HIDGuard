param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "./artifacts",
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"

$rootPath = Resolve-Path "$PSScriptRoot/.."
Set-Location $rootPath

$resolvedOutputDir = Join-Path $rootPath $OutputDir
$appDistDir = Join-Path $rootPath "dist/app"
$simDistDir = Join-Path $rootPath "dist/simulator"
$issScriptPath = Join-Path $rootPath "installer/HIDGuard.iss"

if (!(Test-Path $resolvedOutputDir)) {
    New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null
}

$hasAppBinaries = (Test-Path "$appDistDir/HidGuard.App.exe")
$hasSimBinaries = (Test-Path "$simDistDir/HidGuard.Simulator.exe")
$shouldPublish = (!$SkipPublish) -or (!$hasAppBinaries) -or (!$hasSimBinaries)

if ($shouldPublish) {
    Write-Host "Publishing self-contained application for installer..."
    dotnet publish src/HidGuard.App/HidGuard.App.csproj --configuration $Configuration --runtime win-x64 --self-contained true --output $appDistDir

    Write-Host "Publishing self-contained simulator tool for installer..."
    dotnet publish tools/HidGuard.Simulator/HidGuard.Simulator.csproj --configuration $Configuration --runtime win-x64 --self-contained true --output $simDistDir
}

$isccCmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
$isccCmdPath = if ($isccCmd) { $isccCmd.Source } else { $null }

$isccCandidates = @(
    $isccCmdPath,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe",
    "${env:ChocolateyInstall}\bin\iscc.exe"
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if (![string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
        $isccPath = $candidate
        break
    }
}

if ($isccPath) {
    Write-Host "Compiling installer using Inno Setup compiler: $isccPath"
    $isccArgs = @(
        "/DMyAppVersion=$Version",
        "/DMyAppSourceDir=$appDistDir",
        "/DMySimulatorSourceDir=$simDistDir",
        "/DMyOutputDir=$resolvedOutputDir",
        $issScriptPath
    )

    & $isccPath @isccArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
    }

    $setupFileName = "HIDGuard-Setup-v$Version.exe"
    $setupFilePath = Join-Path $resolvedOutputDir $setupFileName

    if (Test-Path $setupFilePath) {
        $hash = (Get-FileHash -Path $setupFilePath -Algorithm SHA256).Hash
        $checksumPath = Join-Path $resolvedOutputDir "SHA256SUMS.txt"
        "$hash  $setupFileName" | Out-File -FilePath $checksumPath -Encoding utf8 -Append

        Write-Host "Installer package generated successfully."
        Write-Host "Installer: $setupFilePath"
        Write-Host "SHA256:    $hash"
    }
} else {
    Write-Host "Inno Setup compiler (ISCC.exe) not found on system."
    Write-Host "Installer script verified at: $issScriptPath"
    Write-Host "Published binaries ready at:  $appDistDir"
    Write-Host "To build the installer, install Inno Setup 6 (winget install JRSoftware.InnoSetup or choco install innosetup)."
}
