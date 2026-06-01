@echo off
"C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" "UltimateAntiCheat-main\UltimateAntiCheat-main\UltimateAnticheat.sln" /p:Configuration=Release /p:Platform=x64 /m /v:minimal 2>&1 | findstr /i "error"
echo Cikis kodu: %ERRORLEVEL%
