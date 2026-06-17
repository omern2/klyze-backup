@echo off
timeout /t 2 /nobreak >nul
move /y "C:\Users\omery\Music\klyze kayak kodları\exe\Klyze.exe.new" "C:\Users\omery\Music\klyze kayak kodları\exe\Klyze.exe"
start "" "C:\Users\omery\Music\klyze kayak kodları\exe\Klyze.exe"
del "%~f0"
