@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Copy DLLs to Unity Project
echo ========================================
echo.

REM プロジェクトのルートディレクトリ
set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR:~0,-1%

REM ビルド構成（デフォルトはRelease）
set BUILD_CONFIG=Release
if not "%1"=="" set BUILD_CONFIG=%1

echo Build Configuration: %BUILD_CONFIG%
echo.

REM ソースパス
set ANALYZER_DLL=%PROJECT_ROOT%\ExhaustiveSwitch.Analyzer\bin\%BUILD_CONFIG%\netstandard2.0\ExhaustiveSwitch.Analyzer.dll
set ANALYZER_RESOURCES_DLL=%PROJECT_ROOT%\ExhaustiveSwitch.Analyzer\bin\%BUILD_CONFIG%\netstandard2.0\ja\ExhaustiveSwitch.Analyzer.resources.dll
set ATTRIBUTES_DLL=%PROJECT_ROOT%\ExhaustiveSwitch.Attributes\bin\%BUILD_CONFIG%\netstandard2.0\ExhaustiveSwitch.Attributes.dll

REM Unity側の配置先ディレクトリ
set UNITY_ROOT=%PROJECT_ROOT%\..\ExhaustiveSwitch
set UNITY_ANALYZER_DIR=%UNITY_ROOT%\Assets\ExhaustiveSwitch\Analyzer
set UNITY_RESOURCES_DIR=%UNITY_ANALYZER_DIR%\ja

echo ========================================
echo Step 1: Verify source files
echo ========================================

if not exist "%ANALYZER_DLL%" (
    echo [ERROR] Analyzer DLL not found: %ANALYZER_DLL%
    echo Please run build.bat first.
    exit /b 1
)
echo [OK] Found: ExhaustiveSwitch.Analyzer.dll

if not exist "%ANALYZER_RESOURCES_DLL%" (
    echo [WARNING] Japanese resource DLL not found: %ANALYZER_RESOURCES_DLL%
) else (
    echo [OK] Found: ExhaustiveSwitch.Analyzer.resources.dll
)

if not exist "%ATTRIBUTES_DLL%" (
    echo [WARNING] Attributes DLL not found: %ATTRIBUTES_DLL%
) else (
    echo [OK] Found: ExhaustiveSwitch.Attributes.dll
)
echo.

echo ========================================
echo Step 2: Create destination directories
echo ========================================

if not exist "%UNITY_ANALYZER_DIR%" (
    echo Creating directory: %UNITY_ANALYZER_DIR%
    mkdir "%UNITY_ANALYZER_DIR%"
)

if not exist "%UNITY_RESOURCES_DIR%" (
    echo Creating directory: %UNITY_RESOURCES_DIR%
    mkdir "%UNITY_RESOURCES_DIR%"
)
echo.

echo ========================================
echo Step 3: Copy Analyzer DLL
echo ========================================
copy /Y "%ANALYZER_DLL%" "%UNITY_ANALYZER_DIR%\"
if errorlevel 1 (
    echo [ERROR] Failed to copy Analyzer DLL
    exit /b 1
)
echo [OK] Copied: ExhaustiveSwitch.Analyzer.dll
echo.

echo ========================================
echo Step 4: Copy Japanese Resources
echo ========================================
if exist "%ANALYZER_RESOURCES_DLL%" (
    copy /Y "%ANALYZER_RESOURCES_DLL%" "%UNITY_RESOURCES_DIR%\"
    if errorlevel 1 (
        echo [WARNING] Failed to copy Japanese resources
    ) else (
        echo [OK] Copied: ExhaustiveSwitch.Analyzer.resources.dll
    )
) else (
    echo [SKIPPED] Japanese resources not found
)
echo.

echo ========================================
echo Step 5: Copy Attributes DLL (Optional)
echo ========================================
echo NOTE: Attributes DLL is typically included in the Analyzer DLL.
echo If you need to distribute it separately, uncomment the copy command.
echo.
REM if exist "%ATTRIBUTES_DLL%" (
REM     copy /Y "%ATTRIBUTES_DLL%" "%UNITY_ANALYZER_DIR%\"
REM     echo [OK] Copied: ExhaustiveSwitch.Attributes.dll
REM )
echo [SKIPPED] Attributes DLL copy is disabled by default
echo.

echo ========================================
echo Copy Summary
echo ========================================
echo Destination: %UNITY_ANALYZER_DIR%
echo.
echo Copied files:
dir /B "%UNITY_ANALYZER_DIR%\*.dll" 2>nul
echo.
echo Japanese resources:
dir /B "%UNITY_RESOURCES_DIR%\*.dll" 2>nul
echo.

echo ========================================
echo Copy completed successfully!
echo ========================================
echo.
echo NOTE: Unity will automatically reload the DLLs.
echo Check Unity Console for any Analyzer warnings/errors.
echo.

endlocal
