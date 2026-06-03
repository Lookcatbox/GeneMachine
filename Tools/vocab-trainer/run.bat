@echo off
cd /d "%~dp0"
echo Starting AI Vocab Trainer...
python server.py
if errorlevel 1 (
  echo.
  echo Python not found. Install Python 3 or run: py server.py
  pause
)
