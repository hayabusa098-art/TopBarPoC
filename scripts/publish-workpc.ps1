param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$NoZip,
    [switch]$NoClean
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "TopBarPoC.csproj"
$artifactRoot = Join-Path $repoRoot "artifacts\publish\workpc"
$publishDir = Join-Path $artifactRoot "TopBarPoC-workpc-$Runtime"
$zipPath = Join-Path $artifactRoot "TopBarPoC-workpc-$Runtime.zip"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not $NoClean -and (Test-Path $publishDir)) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (-not $NoClean -and (Test-Path $zipPath)) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$packageDocs = @(
    @{ Source = "docs\work-pc-deployment.md"; Destination = "WORKPC_DEPLOYMENT.md" },
    @{ Source = "docs\WORKPC_TRIAL.md"; Destination = "WORKPC_TRIAL.md" }
)
foreach ($packageDoc in $packageDocs) {
    $sourcePath = Join-Path $repoRoot $packageDoc.Source
    if (-not (Test-Path $sourcePath)) {
        throw "Package documentation not found: $sourcePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $publishDir $packageDoc.Destination) -Force
}

if (-not $NoZip) {
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
}

Write-Host "Publish folder: $publishDir"
if (-not $NoZip) {
    Write-Host "Zip package:    $zipPath"
    $zipHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    Write-Host "Zip SHA-256:    $($zipHash.Hash)"
}
