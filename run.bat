@echo off
echo Starting NNAFA...

echo Starting Python Bridge/Processing Layer...
start cmd /k ".\venv\Scripts\activate.bat & python src/03_bridge/server.py"

echo Starting C# WPF Frontend (Standalone)...
"src\04_frontend\bin\Release\net10.0-windows\win-x64\publish\Frontend.exe"

echo System shutdown initiated.
pause
