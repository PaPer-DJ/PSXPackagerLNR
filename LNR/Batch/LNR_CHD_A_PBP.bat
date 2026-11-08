@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
title PSXPackagerLNR - CHD a PBP por lotes

rem Ejecutable en ROMs o en ROMs\.tools\LNR_CHD_A_PBP.
rem Tambien admite arrastrar una carpeta de ROMs sobre el BAT.
set "HERE=%~dp0"
set "ROMDIR=%HERE%"
set "TOOLSDIR=%HERE%.tools\LNR_CHD_A_PBP"

if exist "%HERE%LNR_CHD_A_PBP.ps1" (
    set "TOOLSDIR=%HERE%"
    for %%I in ("%HERE%..\..") do set "ROMDIR=%%~fI"
)
if not "%~1"=="" (
    if exist "%~f1\" set "ROMDIR=%~f1"
)

if not exist "%TOOLSDIR%\LNR_CHD_A_PBP.ps1" goto MISSING
if not exist "%ROMDIR%\" goto MISSINGROM
where powershell.exe >nul 2>&1
if errorlevel 1 goto NOPOWERSHELL

:START
cls
echo ================================================================
echo          PSXPackagerLNR - CHD a PBP DIRECTO
echo ================================================================
echo.
echo ROMs:        "%ROMDIR%"
echo HERRAMIENTAS:"%TOOLSDIR%"
echo DESTINO:     "%ROMDIR%\PBP"
echo.
echo MODO MULTIDISCO:
echo.
echo   1 - SEPARADOS: un PBP por CHD, nombre original conservado.
echo   2 - UNIDOS: un PBP por juego; prioridad a los M3U existentes.
echo       El cambio de disco dependera del emulador.
echo.
choice /C 12 /N /M "Elige 1 o 2: "
if errorlevel 2 (set "MODE=Unidos") else (set "MODE=Separados")

echo.
echo NIVEL DE COMPRESION:
echo   1 - LIGERA
echo   3 - EQUILIBRADA (RECOMENDADA LNR)
echo   5 - ESTANDAR ORIGINAL DE PSXPackager
echo   9 - MAXIMA
echo.
:ASKLEVEL
set "ANSWER="
set /p "ANSWER=Nivel de compresion [3]: "
if not defined ANSWER goto LEVEL3
if "%ANSWER%"=="1" goto LEVEL1
if "%ANSWER%"=="3" goto LEVEL3
if "%ANSWER%"=="5" goto LEVEL5
if "%ANSWER%"=="9" goto LEVEL9
echo [AVISO] Nivel no valido. Selecciona 1, 3, 5 o 9.
goto ASKLEVEL

:LEVEL1
set "LEVEL=1"
goto CONFIRM
:LEVEL3
set "LEVEL=3"
goto CONFIRM
:LEVEL5
set "LEVEL=5"
goto CONFIRM
:LEVEL9
set "LEVEL=9"

:CONFIRM
echo.
echo MODO: %MODE%  /  NIVEL: %LEVEL%
echo Solo se convertiran CHD presentes directamente en la carpeta.
echo Los CHD originales nunca se borran ni modifican.
echo Los PBP existentes no se sobrescriben.
echo.
choice /C SN /N /M "Iniciar conversion [S/N]: "
if errorlevel 2 goto FIN

echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%TOOLSDIR%\LNR_CHD_A_PBP.ps1" -SourceDir "%ROMDIR%" -ToolsDir "%TOOLSDIR%" -MultiMode "%MODE%" -Level %LEVEL%
set "RESULT=%ERRORLEVEL%"
echo.
if "%RESULT%"=="0" (
    echo [OK] Lote terminado. Revisa PBP y los informes en .tools\LNR_CHD_A_PBP\LOGS
) else (
    echo [AVISO] Hubo errores o faltan archivos. Codigo %RESULT%.
    echo Revisa los mensajes anteriores y los informes disponibles.
)
goto FIN

:MISSING
echo ERROR: No se encuentra LNR_CHD_A_PBP.ps1 en "%TOOLSDIR%".
goto FIN
:MISSINGROM
echo ERROR: La carpeta de ROMs no existe: "%ROMDIR%".
goto FIN
:NOPOWERSHELL
echo ERROR: No se encontro Windows PowerShell.
:FIN
echo.
echo ================================================================
echo FIN - Pulsa una tecla para cerrar esta ventana.
echo ================================================================
pause >nul
endlocal
exit /b
