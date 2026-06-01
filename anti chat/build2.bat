@echo off
set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
set VCPATH=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Microsoft\VC\v180\

echo Derleniyor...
%MSBUILD% "UltimateAntiCheat-main\UltimateAntiCheat-main\UltimateAnticheat.sln" /p:Configuration=Release /p:Platform=x64 /p:VCTargetsPath="%VCPATH%" /m /v:minimal

echo.
echo Cikis kodu: %ERRORLEVEL%
if %ERRORLEVEL%==0 (
    echo DERLEME BASARILI!
) else (
    echo DERLEME HATALI!
)
pause
