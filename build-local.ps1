param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$publishDir = ".\publish\$Runtime"
$installerDir = ".\publish\installer"
$bundleExe = Join-Path $installerDir "VortexModLists-Setup.exe"
$runtimeInstaller = Join-Path $installerDir "windowsdesktop-runtime-10.0.0-win-x64.exe"
$runtimeDownloadUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.0/windowsdesktop-runtime-10.0.0-win-x64.exe"

Write-Host "[1/7] Restore..."
dotnet restore

Write-Host "[2/7] Build ($Configuration)..."
dotnet build -c $Configuration --no-restore

Write-Host "[3/7] Publish ($Configuration, $Runtime, framework-dependent)..."
dotnet publish .\VortexModLists.csproj `
  -c $Configuration `
  -r $Runtime `
  --self-contained false `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $publishDir

Write-Host "[4/7] Download .NET Desktop Runtime installer..."
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
Invoke-WebRequest -Uri $runtimeDownloadUrl -OutFile $runtimeInstaller

Write-Host "[5/7] Build MSI installer..."
dotnet build .\installer\VortexModLists.Installer.Msi\VortexModLists.Installer.Msi.wixproj `
  -c $Configuration `
  -p:PublishDir=$(Resolve-Path $publishDir)

Write-Host "[6/7] Build bootstrapper EXE..."
dotnet build .\installer\VortexModLists.Installer.Bundle\VortexModLists.Installer.Bundle.wixproj `
  -c $Configuration `
  -p:DotNetDesktopRuntimePath=$(Resolve-Path $runtimeInstaller)

Write-Host "[7/7] Done"
Write-Host "Publish output: $publishDir"
Write-Host "Installer output: $installerDir"
Write-Host "Bootstrapper EXE: $bundleExe"