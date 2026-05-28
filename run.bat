@echo off
cd /d "%~dp0"
set LSLAPICFG=
echo Starting NNAFA...

rem Extract frontend assets if not already extracted
if not exist "deploy\frontend\Frontend.exe" (
    echo Extracting frontend assets...
    powershell -Command "Expand-Archive -Path deploy\frontend.zip -DestinationPath deploy\frontend -Force"
)

echo Starting Python Bridge/Processing Layer...
start cmd /k ".\venv\Scripts\python.exe src/03_bridge/server.py"

echo Starting C# WPF Frontend (Standalone)...
"deploy\frontend\Frontend.exe"

rem Cleanup (optional)
rem rmdir /s /q deploy\frontend

echo System shutdown initiated.
pause
