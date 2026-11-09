@echo off
rem Underworld Revisited - widescreen weapons, step 2: your painted pictures into a pack.
rem Double-click it after painting. The pack holds only what you painted, never the original's pixels.
setlocal
cd /d "%~dp0"
set "TOOL=%~dp0uwdump.exe"
if not exist "%TOOL%" set "TOOL=%~dp0..\uwdump.exe"
if not exist "%TOOL%" (
  echo uwdump.exe was not found, neither beside this file nor in the folder above it.
  echo Keep this folder inside the Translation Kit.
  goto end
)
set "WORK=%~dp0Work"
set "GAME="
if not "%~1"=="" set "GAME=%~1"
if not defined GAME if exist "%WORK%\game.txt" set /p GAME=<"%WORK%\game.txt"
set "GAMEARG="
if defined GAME set GAMEARG="%GAME%"

if not exist "%WORK%\painted" (
  echo There is nothing to make a pack of yet - run "1 Start painting" first.
  goto end
)

"%TOOL%" pack-strip %GAMEARG% --from "%WORK%\painted" --out "%WORK%\pack" >"%WORK%\report.txt" 2>&1
echo.>>"%WORK%\report.txt"
"%TOOL%" pack-check %GAMEARG% --pack "%WORK%\pack" >>"%WORK%\report.txt" 2>&1
type "%WORK%\report.txt"

echo.
echo The pack is the folder Work\pack, this report is Work\report.txt.
echo Put your name and the pack's id into Work\pack\mod.json once.
echo To try it, copy the folder pack into the game's folder Mods (the Game window's Mods button
echo opens it) and load a game. Run this again after painting more.

:end
echo.
pause
