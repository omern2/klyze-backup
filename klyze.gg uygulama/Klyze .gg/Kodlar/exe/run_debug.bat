@echo off
cd /d "%~dp0"
"%~dp0Klyze.exe" > "%~dp0debug_output.txt" 2>&1
