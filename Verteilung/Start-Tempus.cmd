@echo off
setlocal
cd /d "%~dp0"
if not exist "Daten" mkdir "Daten"
if not exist "Daten\tempus.sqlite3" if not exist "Daten\saldo.sqlite3" (
    copy /y "Demodaten\tempus.sqlite3" "Daten\tempus.sqlite3" >nul
    if errorlevel 1 goto failure
)
"Programm\Tempus.exe" --DataDir "%CD%\Daten" %*
if errorlevel 1 goto failure
exit /b 0
:failure
echo Start fehlgeschlagen. Bitte das gesamte ZIP in einen beschreibbaren Ordner entpacken.
pause
exit /b 1
