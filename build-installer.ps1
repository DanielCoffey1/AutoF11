# Build script for AutoF11 installer
# This script publishes the application and then builds the InnoSetup installer

param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "Building AutoF11 installer..." -ForegroundColor Green

# Step 1: Clean previous builds
Write-Host "`n[1/3] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path "bin\$Configuration\net8.0-windows\$RuntimeIdentifier\publish") {
    Remove-Item "bin\$Configuration\net8.0-windows\$RuntimeIdentifier\publish" -Recurse -Force
}
if (Test-Path "dist") {
    Remove-Item "dist" -Recurse -Force
}

# Step 2: Publish the application as self-contained
Write-Host "`n[2/3] Publishing application (self-contained with .NET 8 runtime)..." -ForegroundColor Yellow
dotnet publish -c $Configuration -r $RuntimeIdentifier --self-contained true -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nError: Failed to publish the application!" -ForegroundColor Red
    exit 1
}

# Verify publish directory exists
$publishDir = "bin\$Configuration\net8.0-windows\$RuntimeIdentifier\publish"
if (-not (Test-Path $publishDir)) {
    Write-Host "`nError: Publish directory not found: $publishDir" -ForegroundColor Red
    exit 1
}

Write-Host "Publish successful! Files are in: $publishDir" -ForegroundColor Green

# Step 3: Build the InnoSetup installer
Write-Host "`n[3/3] Building InnoSetup installer..." -ForegroundColor Yellow

# Try to find InnoSetup compiler
$innoCompiler = $null
$possiblePaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $innoCompiler = $path
        break
    }
}

if ($null -eq $innoCompiler) {
    Write-Host "`nError: InnoSetup compiler (ISCC.exe) not found!" -ForegroundColor Red
    Write-Host "Please install InnoSetup from https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Or manually compile setup.iss using InnoSetup Compiler" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found InnoSetup compiler at: $innoCompiler" -ForegroundColor Green

# Compile the installer
& $innoCompiler "setup.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nError: Failed to build the installer!" -ForegroundColor Red
    exit 1
}

Write-Host "`n✓ Installer built successfully!" -ForegroundColor Green
Write-Host "Installer location: dist\AutoF11-Setup-1.0.0.exe" -ForegroundColor Cyan

