@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

rem ---- 1. find the Valheim game folder ----
set "VDIR="
for %%D in ("C:\Program Files (x86)\Steam\steamapps\common\Valheim" "C:\Program Files\Steam\steamapps\common\Valheim" "D:\SteamLibrary\steamapps\common\Valheim" "D:\Steam\steamapps\common\Valheim" "E:\SteamLibrary\steamapps\common\Valheim") do (
  if not defined VDIR if exist "%%~D\valheim_Data\Managed\assembly_valheim.dll" set "VDIR=%%~D"
)
if not defined VDIR (
  echo Could not find Valheim automatically.
  set /p VDIR=Paste your Valheim folder path, no trailing backslash: 
)
echo Valheim: !VDIR!

rem ---- 2. find BepInEx core: game folder first, then r2modman / Thunderstore profiles ----
set "BCORE="
if exist "!VDIR!\BepInEx\core\BepInEx.dll" set "BCORE=!VDIR!\BepInEx\core"
if not defined BCORE for /d %%P in ("%APPDATA%\r2modmanPlus-local\Valheim\profiles\*") do (
  if not defined BCORE if exist "%%~P\BepInEx\core\BepInEx.dll" set "BCORE=%%~P\BepInEx\core" & set "PLUGDIR=%%~P\BepInEx\plugins"
)
if not defined BCORE for /d %%P in ("%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\*") do (
  if not defined BCORE if exist "%%~P\BepInEx\core\BepInEx.dll" set "BCORE=%%~P\BepInEx\core" & set "PLUGDIR=%%~P\BepInEx\plugins"
)
if not defined BCORE (
  echo BepInEx.dll not found in the game folder or in r2modman/Thunderstore profiles.
  echo Install BepInEx first, run the game once, then re-run this script.
  set /p BCORE=Or paste the full path to a BepInEx\core folder: 
)
if not defined PLUGDIR set "PLUGDIR=!VDIR!\BepInEx\plugins"
echo BepInEx core: !BCORE!

rem ---- 3. build ----
dotnet build -c Release "-p:ValheimDir=!VDIR!" "-p:BepInExCore=!BCORE!"
if errorlevel 1 ( echo. & echo BUILD FAILED - copy the output above and send it back. & pause & exit /b 1 )

rem ---- 4. install ----
set "OUT=!PLUGDIR!\PortalSoundControl"
mkdir "!OUT!" 2>nul
copy /y "bin\Release\net48\PortalSoundControl.dll" "!OUT!\" >nul
echo.
echo Installed to: !OUT!
echo Launch Valheim, then search BepInEx\LogOutput.log for "Portal Sound Control".
pause
