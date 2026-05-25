@echo off
echo Starting NNAFA...

echo Starting Python Bridge/Processing Layer...
start cmd /k ".\venv\Scripts\activate.bat & python src/03_bridge/server.py"

echo Starting C# WPF Frontend (Standalone)...
"deploy\frontend\Frontend.exe"

echo System shutdown initiated.
pause
