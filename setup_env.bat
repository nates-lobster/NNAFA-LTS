@echo off
setlocal enabledelayedexpansion

echo ============================================================
echo      NNAFA V0.2 - Automated Environment Setup
echo ============================================================
echo.

:: Step 1: Verify Python Installation
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Python is not installed or not in your system PATH.
    echo Please install Python 3.11+ ^(64-bit^) and check "Add Python to PATH" during installation.
    echo Download link: https://www.python.org/downloads/
    echo.
    pause
    exit /b 1
)

:: Step 2: Create Virtual Environment if it doesn't exist
if not exist "venv" (
    echo Creating Python virtual environment ^(venv^)...
    python -m venv venv
    if !errorlevel! neq 0 (
        echo [ERROR] Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo Virtual environment created successfully.
) else (
    echo Virtual environment ^(venv^) already exists. Skipping creation.
)
echo.

:: Step 3: Upgrade pip inside venv
echo Upgrading pip inside virtual environment...
.\venv\Scripts\python.exe -m pip install --upgrade pip
if %errorlevel% neq 0 (
    echo [WARNING] Failed to upgrade pip. Continuing...
)
echo.

:: Step 4: Install packages from requirements.txt
echo Installing requirements (numpy, scipy, pylsl, brainflow, websockets, protobuf)...
.\venv\Scripts\python.exe -m pip install -r requirements.txt
if %errorlevel% neq 0 (
    echo [ERROR] Failed to install requirements. Please check your internet connection and try again.
    pause
    exit /b 1
)
echo.

:: Step 5: Extract Standalone Frontend
if not exist "deploy\frontend\Frontend.exe" (
    echo Extracting standalone C# WPF Frontend...
    powershell -Command "Expand-Archive -Path deploy.zip -DestinationPath . -Force"
    if %errorlevel% neq 0 (
        echo [WARNING] Failed to extract deploy.zip. You may need to extract it manually.
    )
) else (
    echo Standalone frontend already extracted.
)
echo.
echo ============================================================
echo   Setup completed successfully!
echo   You can now launch the full NNAFA application using:
echo   .\run.bat
echo ============================================================
echo.
pause
