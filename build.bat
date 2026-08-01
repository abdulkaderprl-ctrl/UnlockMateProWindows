@echo off
title Building Unlock Mate Pro (.NET 8 WPF)
echo ========================================================
echo        Building Unlock Mate Pro Desktop Application
echo ========================================================

where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Error: .NET SDK is not found in System PATH.
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo Cleaning previous build artifacts...
dotnet clean -c Release

echo.
echo Restoring NuGet packages and dependencies...
dotnet restore

echo.
echo Compiling solution in Release mode...
dotnet build -c Release -r win-x64

if %ERRORLEVEL% NEQ 0 (
    echo Build failed! Please check compiler errors above.
    pause
    exit /b 1
)

echo.
echo Publishing framework-dependent Release binaries...
dotnet publish -c Release -r win-x64 --self-contained true -o bin\Release\net8.0-windows\publish

echo.
echo ========================================================
echo Build Succeeded! Binaries saved in bin\Release\net8.0-windows\publish\
echo ========================================================
pause
