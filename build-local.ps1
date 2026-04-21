param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

Write-Host "[1/5] Restore..."
dotnet restore

Write-Host "[2/5] Build ($Configuration)..."
dotnet build -c $Configuration --no-restore

$publishDir = ".\publish\$Runtime"
$zipPath = ".\publish\VortexModLists-$Runtime.zip"

Write-Host "[3/5] Publish ($Configuration, $Runtime, self-contained, single-file)..."
dotnet publish .\VortexModLists.csproj `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $publishDir

Write-Host "[4/5] Create ZIP..."
Get-ChildItem -Path $publishDir -Filter *.pdb -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

Write-Host "[5/5] Done"
Write-Host "Publish output: $publishDir"
Write-Host "ZIP package:    $zipPath"