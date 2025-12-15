@echo off
setlocal enabledelayedexpansion

echo ========================================
echo ExhaustiveSwitch Analyzer Build Script
echo ========================================
echo.

REM プロジェクトのルートディレクトリ
set PROJECT_ROOT=%~dp0

REM ビルド構成（デフォルトはRelease）
set BUILD_CONFIG=Release
if not "%1"=="" set BUILD_CONFIG=%1

echo Build Configuration: %BUILD_CONFIG%
echo Project Root: %PROJECT_ROOT%
echo.

REM ソリューションファイルのパス
set SOLUTION=%PROJECT_ROOT%ExhaustiveSwitch.sln

echo ========================================
echo Step 1: Restore NuGet packages
echo ========================================
dotnet restore "%SOLUTION%"
if errorlevel 1 (
    echo [ERROR] Failed to restore NuGet packages.
    exit /b 1
)
echo.

echo ========================================
echo Step 2: Build solution
echo ========================================
dotnet build "%SOLUTION%" --configuration %BUILD_CONFIG% --no-restore
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)
echo.

echo ========================================
echo Step 3: Run tests
echo ========================================
dotnet test "%SOLUTION%" --configuration %BUILD_CONFIG% --no-build --verbosity minimal
if errorlevel 1 (
    echo [ERROR] Tests failed.
    exit /b 1
)
echo.

echo ========================================
echo Build Summary
echo ========================================
echo Configuration: %BUILD_CONFIG%
echo.
echo Output DLLs:
echo - ExhaustiveSwitch.Analyzer.dll
echo   ^> %PROJECT_ROOT%ExhaustiveSwitch.Analyzer\bin\%BUILD_CONFIG%\netstandard2.0\
echo.
echo - ExhaustiveSwitch.Attributes.dll
echo   ^> %PROJECT_ROOT%ExhaustiveSwitch.Attributes\bin\%BUILD_CONFIG%\netstandard2.0\
echo.
echo - Japanese Resources
echo   ^> %PROJECT_ROOT%ExhaustiveSwitch.Analyzer\bin\%BUILD_CONFIG%\netstandard2.0\ja\
echo.

echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Next step: Run copy-to-unity.bat to copy DLLs to Unity project
echo.

endlocal
