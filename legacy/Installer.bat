@echo off
setlocal
REM --------------------------------------------------------------
REM Startet Installer.ps1 mit umgangener Ausfuehrungsrichtlinie.
REM Das gilt nur fuer diesen einen Aufruf, am System wird nichts
REM dauerhaft geaendert. Adminrechte fordert das Skript selbst an.
REM --------------------------------------------------------------

set "PS1_FILE=%~dp0Installer.ps1"

if not exist "%PS1_FILE%" (
  echo Fehler: "%PS1_FILE%" wurde nicht gefunden.
  pause
  exit /b 2
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%PS1_FILE%"
set "RC=%ERRORLEVEL%"

if "%RC%"=="0" exit /b 0

echo.
if "%RC%"=="1223" (
  echo Abgebrochen: Die Administratorrechte wurden nicht erteilt.
) else (
  echo Fehler: Das PowerShell-Skript wurde mit Code %RC% beendet.
)
pause
exit /b %RC%
