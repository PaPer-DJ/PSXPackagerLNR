# PSXPackagerLNR - Conversor CHD -> PBP por lotes (Windows)

Esta utilidad es una adaptacion de automatizacion alrededor de PSXPackager:
https://github.com/RupertAvery/PSXPackager
El software original, su autoria y la licencia MIT se conservan. LaNaveRETRO
no se atribuye la creacion de PSXPackager.

## Instalacion portable

Descarga el artefacto de GitHub Actions y extrae el fichero
PSXPackagerLNR-Toolkit-win-x64.zip directamente en tu carpeta de juegos.

    C:\roms\psx\
      LNR_CHD_A_PBP.bat          <- Lanzar aqui
      Juego 1.chd
      Juego 2.chd
      Juego multidisco.m3u
      .tools\
        LNR_CHD_A_PBP\
          LNR_CHD_A_PBP.bat      <- Tambien se puede lanzar aqui
          LNR_CHD_A_PBP.ps1
          psxpackager.exe       <- No necesita CHDMAN
          Resources\gameInfo.db
          LOGS\                 <- Informes automaticos
          _TEMP\                <- PBP temporales
      PBP\
        Juego 1.pbp
        Juego 2.pbp

- Ejecuta cualquiera de los dos BAT. Ambos toman por defecto como origen
  la carpeta de ROMs que contiene .tools.
- Puedes arrastrar una carpeta de CHD sobre el BAT: se procesaran
  sus CHD y los PBP se guardaran en la subcarpeta PBP.
- Solo procesa CHD de la carpeta elegida (no recursivamente).
- No borra nunca los CHD, M3U ni SBI originales.

## Opciones del menu

- 1 - Discos separados: cada CHD -> un PBP con identico nombre base.
  Por ejemplo: Juego - CD2.chd -> Juego - CD2.pbp.
- 2 - Discos unidos: prioridad a M3U existentes, respetando su orden.
  Si no existen, intenta agrupar nombres terminados en CD1, CD2,
  (Disc 1), (Disc 2), etc. Solo agrupa series completas de 2 a 5
  discos consecutivos. Un M3U incompleto o ambiguo se omite con aviso
  para evitar generar un juego mutilado.
- Compresion 1 (minima), 5 (normal y valor predeterminado en el programa),
  9 (maxima).

## Seguridad

- Los PBP existentes no se sobrescriben.
- Convierte CHD directamente a PBP, sin BIN/CUE intermedios.
- Cada conversion se prepara en un directorio temporal independiente
  dentro de las herramientas.
- Comprueba salida de la CLI, tamano minimo y cabecera PBP.
- Mueve el PBP correcto a la carpeta PBP tras las comprobaciones.
- Limpia los temporales tanto si el trabajo se completa como si falla.
- Los informes se guardan en .tools\LNR_CHD_A_PBP\LOGS.
- Muestra tamano CHD, tamano PBP y variacion por juego y para el lote.
  Los MB siguen la convencion de Windows (1 MB = 1 MiB = 1048576 bytes).
- Copia ficheros SBI externos si existen, pero el uso de SBI con un
  PBP multidisco dependera del emulador. No garantiza eludir protecciones.
- El cambio de disco en un PBP unido tambien depende del emulador de destino.

## Prueba del plan sin convertir

Desde PowerShell, dentro de la carpeta de ROMs:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".tools\LNR_CHD_A_PBP\LNR_CHD_A_PBP.ps1" -SourceDir "C:\roms\psx" -ToolsDir "C:\roms\psx\.tools\LNR_CHD_A_PBP" -MultiMode Unidos -Level 5 -PlanOnly

El workflow prueba el planificador de ambos modos con nombres ficticios.
Esto no sustituye una prueba real con imagenes CHD.

## Pruebas recomendadas

1. CHD simple: verificar que el PBP arranca y conserva su nombre.
2. Tomb Raider (1996): comprobar musica y pistas de audio CD.
3. CHD multidisco con M3U: comprobar el cambio de disco.
4. Comparar PBP y BIN/CUE en una consola de emulacion poco potente.

No se incluyen juegos comerciales, BIOS ni imagenes protegidas.

## Desarrollo

Los scripts se mantienen en LNR/Batch/. GitHub Actions genera tres ZIP:
CLI, GUI y Toolkit portable con los scripts y el CLI. La personalizacion
visual de la GUI quedara para una fase posterior.
