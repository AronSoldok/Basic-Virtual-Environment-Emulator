$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path $PSScriptRoot -Parent
$LocalSdk = Join-Path $env:LOCALAPPDATA "dotnet-sdk\dotnet.exe"
$Dotnet = $null

if (Test-Path $LocalSdk) {
    $Dotnet = $LocalSdk
    $env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "dotnet-sdk"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
} else {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $Dotnet = $cmd.Source }
}

if (-not $Dotnet) {
    throw "NET 8 SDK not found. System dotnet 3.1 cannot build this project. Install SDK 8 or unpack it to %LOCALAPPDATA%\dotnet-sdk"
}

$csproj = Join-Path $RepoRoot "src\ClosedEnv\ClosedEnv.csproj"
$staging = Join-Path $RepoRoot "obj\publish-win-x64"
$exeDir = Join-Path $RepoRoot "exe"

Write-Host "SDK: $Dotnet"
& $Dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $staging

if ($LASTEXITCODE -ne 0) {
    throw "publish failed with exit code $LASTEXITCODE"
}

$published = Join-Path $staging "ClosedEnv.exe"
if (-not (Test-Path $published)) {
    throw "Published exe not found: $published"
}

New-Item -ItemType Directory -Force -Path $exeDir | Out-Null
Copy-Item -Path $published -Destination (Join-Path $exeDir "ClosedEnv.exe") -Force
Copy-Item -Path $published -Destination (Join-Path $exeDir "ClosedEnv-Web.exe") -Force

Write-Host ""
Write-Host "Ready:"
Write-Host "  $(Join-Path $exeDir 'ClosedEnv.exe')"
Write-Host "  $(Join-Path $exeDir 'ClosedEnv-Web.exe')"
