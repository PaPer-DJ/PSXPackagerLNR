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
echo COMO USARLO:
echo   1. Copia este Toolkit dentro de una carpeta con juegos .CHD,
echo      o arrastra otra carpeta de CHD al archivo BAT.
echo   2. Elige si quieres un PBP por disco o un PBP multidisco.
echo      En modo Unidos se respetan los M3U existentes.
echo   3. Escoge la compresion y cuantos juegos procesar a la vez.
echo   4. Los resultados se guardaran en la carpeta PBP indicada.
echo.
echo SEGURIDAD: los CHD, M3U y SBI originales se conservan SIEMPRE.
echo No se sobrescriben PBP existentes y no se necesita CHDMAN.
echo Se guardara un informe de actividad en .tools\LNR_CHD_A_PBP\LOGS.
echo.
echo MODO MULTIDISCO:
echo.
echo   1 - SEPARADOS: un PBP por CHD, nombre original conservado. [RECOMENDADO]
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
goto ASKPARALLEL
:LEVEL3
set "LEVEL=3"
goto ASKPARALLEL
:LEVEL5
set "LEVEL=5"
goto ASKPARALLEL
:LEVEL9
set "LEVEL=9"

:ASKPARALLEL
echo.
echo CONVERSIONES SIMULTANEAS:
echo   1 - SECUENCIAL: un juego cada vez.
echo   2 - DOS juegos a la vez.
echo   3 - TRES juegos a la vez.
echo   4 - RECOMENDADO PARA HDD [por defecto].
echo   6 - SEIS juegos a la vez.
echo   8 - RECOMENDADO PARA SSD [requiere recursos suficientes].
echo   0 - AUTO: entre 1 y 4 conversiones segun la CPU.
echo.
echo Mas conversiones necesitan mas RAM y espacio temporal.
echo La velocidad real depende del equipo y del tipo de almacenamiento.
echo.
set "PARALLEL="
set /p "PARALLEL=Numero de conversiones [4]: "
if not defined PARALLEL set "PARALLEL=4"
if /I "%PARALLEL%"=="A" set "PARALLEL=0"
if /I "%PARALLEL%"=="AUTO" set "PARALLEL=0"
for %%N in (0 1 2 3 4 6 8) do if "%PARALLEL%"=="%%N" goto CONFIRM
echo [AVISO] Opcion no valida: elige 1, 2, 3, 4, 6, 8 o 0 para Auto.
goto ASKPARALLEL

:CONFIRM
echo.
echo MODO: %MODE%  /  NIVEL: %LEVEL%  /  SIMULTANEAS: %PARALLEL% (0 = Auto)
echo Solo se convertiran CHD presentes directamente en la carpeta.
echo Los CHD originales nunca se borran ni modifican.
echo Los PBP existentes no se sobrescriben.
echo.
choice /C SN /N /M "Iniciar conversion [S/N]: "
if errorlevel 2 goto FIN

echo.
echo ================================================================
echo CONTROLES DURANTE LA CONVERSION (EN ESTA VENTANA):
echo   P  PAUSAR: termina los juegos activos antes de detener el lote.
echo   R  REANUDAR: continua los juegos pendientes.
echo   C  CANCELAR: detiene el lote y sus conversiones activas.
echo       Los originales CHD siempre se conservan.
echo ================================================================
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%TOOLSDIR%\LNR_CHD_A_PBP.ps1" -SourceDir "%ROMDIR%" -ToolsDir "%TOOLSDIR%" -MultiMode "%MODE%" -Level %LEVEL% -Parallel %PARALLEL%
set "RESULT=%ERRORLEVEL%"
echo.
if "%RESULT%"=="0" (
    echo [OK] Lote terminado. Revisa PBP y los informes en .tools\LNR_CHD_A_PBP\LOGS
) else if "%RESULT%"=="3" (
    echo [AVISO] Lote cancelado. Los PBP completados se conservan.
    echo Los CHD originales no se han borrado.
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
