@echo off
echo Starting NNAFA V0.2...

echo Starting Python Bridge/Processing Layer...
start cmd /k ".\venv\Scripts\activate.bat & python src/03_bridge/server.py"

echo Starting C# WPF Frontend...
cd src\04_frontend
dotnet run

echo System shutdown initiated.
pause
