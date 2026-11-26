@echo off
setlocal
cd /d "%~dp0"
if not exist "Daten" mkdir "Daten"
if not exist "Daten\tempus.sqlite3" if not exist "Daten\saldo.sqlite3" (
    copy /y "Demodaten\tempus.sqlite3" "Daten\tempus.sqlite3" >nul
    if errorlevel 1 goto failure
)
dotnet run --project "Tempus.Web" -- --DataDir "%CD%\Daten"
if errorlevel 1 goto failure
exit /b 0
:failure
echo Start fehlgeschlagen. Bitte .NET 10 SDK und Schreibrechte im Projektordner pruefen.
pause
exit /b 1
