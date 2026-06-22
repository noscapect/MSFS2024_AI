@echo off
setlocal
cd /d "%~dp0"
title MSFS 2024 AI - Before Start Monitor
echo Starting the passive Before Start monitor...
echo Leave this window open and operate the controls manually in MSFS.
echo The monitor stops automatically after five minutes.
echo.
".\src\SimConnectProbe\bin\Release\net472\SimConnectProbe.exe" monitor-before-start
echo.
echo Monitor finished. Press any key to close this window.
pause >nul
