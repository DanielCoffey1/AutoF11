@echo off
REM Build script for AutoF11 installer (Windows Batch)
REM This script publishes the application and then builds the InnoSetup installer

echo Building AutoF11 installer...

REM Step 1: Clean previous builds
echo.
echo [1/3] Cleaning previous builds...
if exist "bin\Release\net8.0-windows\win-x64\publish" (
    rmdir /s /q "bin\Release\net8.0-windows\win-x64\publish"
)
if exist "dist" (
    rmdir /s /q "dist"
)

REM Step 2: Publish the application as self-contained
echo.
echo [2/3] Publishing application (self-contained with .NET 8 runtime)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

if errorlevel 1 (
    echo.
    echo Error: Failed to publish the application!
    exit /b 1
)

REM Verify publish directory exists
if not exist "bin\Release\net8.0-windows\win-x64\publish" (
    echo.
    echo Error: Publish directory not found!
    exit /b 1
)

echo Publish successful!

REM Step 3: Build the InnoSetup installer
echo.
echo [3/3] Building InnoSetup installer...

REM Try to find InnoSetup compiler
set INNO_COMPILER=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set INNO_COMPILER=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set INNO_COMPILER=%ProgramFiles%\Inno Setup 6\ISCC.exe
) else if exist "%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe" (
    set INNO_COMPILER=%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe
) else if exist "%ProgramFiles%\Inno Setup 5\ISCC.exe" (
    set INNO_COMPILER=%ProgramFiles%\Inno Setup 5\ISCC.exe
)

if "%INNO_COMPILER%"=="" (
    echo.
    echo Error: InnoSetup compiler (ISCC.exe) not found!
    echo Please install InnoSetup from https://jrsoftware.org/isdl.php
    echo Or manually compile setup.iss using InnoSetup Compiler
    exit /b 1
)

echo Found InnoSetup compiler at: %INNO_COMPILER%

REM Compile the installer
"%INNO_COMPILER%" setup.iss

if errorlevel 1 (
    echo.
    echo Error: Failed to build the installer!
    exit /b 1
)

echo.
echo Installer built successfully!
echo Installer location: dist\AutoF11-Setup-1.0.0.exe

