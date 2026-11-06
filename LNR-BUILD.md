# PSXPackagerLNR — primera compilación Windows

Este repositorio es un fork de [RupertAvery/PSXPackager](https://github.com/RupertAvery/PSXPackager). El software original y su código fuente no son una creación de LaNaveRETRO. Se mantiene la licencia MIT y los avisos de autoría correspondientes.

## Objetivo de esta rama

Compilar el **código fuente actual** para Windows x64 y obtener:
- `PSXPackagerLNR-CLI-win-x64.zip`: aplicación de línea de comandos (ejecutable original `psxpackager.exe`).
- `PSXPackagerLNR-GUI-win-x64.zip`: aplicación gráfica (ejecutable original `PSXPackagerGUI.exe`).

Los nombres de los ejecutables **no se han cambiado aún**; esta primera fase comprueba la compilación del código original. Los paquetes llevan la marca LNR para distinguirlos.

La rama del código fuente incluye un lector de CHD: `PSXPackager.Common/Chd`, y la CLI admite `.chd` como entrada. Esto **no implica que todos los CHD o todos los emuladores sean compatibles**.

## Cómo compilar desde GitHub

1. Ve a la pestaña **Actions**.
2. Abre **LNR Windows CHD-to-PBP builds**.
3. Selecciona **Run workflow**, rama `lnr/windows-chd-build`.
4. Cuando termine correctamente, descarga el artefacto **PSXPackagerLNR-Windows-x64**, que contiene los dos ZIP.

El flujo publica ejecutables **autocontenidos**, de modo que no es necesario instalar .NET 8 por separado en la máquina destino. Si la compilación falla, revisa el log del trabajo antes de distribuirlos.

## Conversión directa de CHD en la CLI

Ejemplo para un disco:

```bat
psxpackager.exe -i "C:\roms\psx\Tekken 3 (USA).chd" -o "C:\roms\psx\PBP" -l 1
```

Para conservar el nombre se utiliza por defecto `%FILENAME%`, cambiando solo la extensión.

**Multidisco separado:** convertir cada `.chd` individualmente. **Multidisco unido:** utilizar un archivo `.m3u` con los CHD en orden, si la compilación confirma el soporte. Revisa también los ficheros `.sbi`: PBP no es un contenedor universal para subcanales/protecciones.

## Pruebas antes de usar con una biblioteca grande

- CHD simple con audio CD: convertir, arrancar y verificar música.
- CHD multidisco separado: verificar cambio de disco en el emulador.
- M3U con CHD: verificar creación y selección de discos desde el emulador.
- Comparar unos minutos de rendimiento y audio en SF3500 con BIN/CUE.
- Verificar nombres y que nunca se sobrescriban archivos anteriores.

Este workflow valida la presencia del ejecutable, los recursos y la ayuda de la CLI, **pero no prueba la conversión efectiva con una imagen CHD**.

## Próximas mejoras LNR

Menú para elegir discos unidos o separados, procesamiento por lotes desde `.tools\LNR_CHD_A_PBP`, gestión de PBP existentes, registro de errores y personalización opcional de la GUI.

## Licencia y créditos

Respetar `LICENSE` y todos los créditos del proyecto original. La adaptación y automatización de compilación LNR no implican autoría del software de origen.
