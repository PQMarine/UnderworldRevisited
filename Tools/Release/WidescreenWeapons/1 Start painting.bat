@echo off
rem Underworld Revisited - widescreen weapons, step 1: a folder to paint in.
rem Double-click it. If your game is not found, drop the game's folder onto this file.
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

mkdir "%WORK%" 2>nul

rem Your pictures are never overwritten; the guides and the palette are written anew each time.
if exist "%WORK%\painted" (
  echo The folder Work already holds your pictures - they stay as they are.
  echo Writing the guides and the palette anew ...
) else (
  echo Writing the weapon pictures, the guides and the palette into the folder Work ...
)

"%TOOL%" weapon-guide %GAMEARG% --out "%WORK%\guide"
if errorlevel 1 (
  echo.
  echo The game was not found. Drop the game's folder ^(the one with game.gog or game.bin^) onto this file.
  goto end
)
if defined GAME >"%WORK%\game.txt" echo %GAME%
"%TOOL%" palette %GAMEARG% --out "%WORK%\palette"
if not exist "%WORK%\painted" "%TOOL%" weapon-frames %GAMEARG% --out "%WORK%\painted"

echo.
echo Done. In the folder Work:
echo   guide    where each picture lies on a wide screen; edges.txt lists the ones to paint
echo   painted  the pictures - paint on these, then run "2 Make pack"
echo   palette  the game's colours for your paint program
start "" explorer "%WORK%"

:end
echo.
pause
