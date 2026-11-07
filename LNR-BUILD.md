# PSXPackagerLNR — compilación Windows y ejecutables

Este repositorio es un fork de [RupertAvery/PSXPackager](https://github.com/RupertAvery/PSXPackager). LaNaveRETRO no creó el motor original. Se conserva la licencia MIT y la autoría original.

## Compilación Windows x64 y distribución

GitHub Actions genera tres ZIP con versión y número de compilación:

- **GUI-win-x64.zip** (recomendado): incorpora
  **`PSXPackagerLNR.exe`**, un **lanzador nativo Win32 muy pequeño**
  que abre directamente la GUI del conversor LNR CHD→PBP por lotes,
  y **`PSXPackagerGUI.exe`**, que contiene el código completo
  de la aplicación y permite acceder a la interfaz clásica original.
  Ambos usan la misma GUI y el motor de conversión: el lanzador
  no incluye otra copia de .NET.
- **CLI-win-x64.zip**: programa de línea de comandos `psxpackager.exe`.
- **Toolkit-win-x64.zip**: scripts portables BAT y PowerShell.

La compilación GUI incluye además el motor `psxpackager.exe`,
la base de datos, créditos, licencia y manuales. **No borres ni
muevas archivos individuales**: extrae todo el ZIP y ejecuta
`PSXPackagerLNR.exe`. Ambas interfaces usan el mismo motor y
la conversión CHD→PBP no necesita CHDMAN.

La aplicación principal se publica con .NET 8 **autocontenido y
SingleFile** para Windows x64. **Solo se distribuye una copia
completa de esa aplicación**. Aparte se compila un pequeño ejecutable
de C nativo (`LNR/Launcher/PSXPackagerLNR.c`) con MSVC y un
icono del proyecto original. Este lanzador llama a
`PSXPackagerGUI.exe --lnr-batch`, que selecciona la ventana de
conversión por lotes desde `App.OnStartup`. Si se abre
`PSXPackagerGUI.exe` normalmente, aparece la interfaz clásica.

La compilación comprueba que el lanzador mida **menos de 1 MB**
(y que el ZIP incluya ambos EXE gráficos más `psxpackager.exe`).
**No precisa instalar .NET adicional** en la máquina destino,
pero es necesario conservar `PSXPackagerGUI.exe` y el resto
del paquete al lado del lanzador. Una copia suelta del lanzador
no puede sustituir a toda la aplicación.

La rama del código fuente incluye un lector de CHD: `PSXPackager.Common/Chd`, y la CLI admite `.chd` como entrada. Esto **no implica que todos los CHD o todos los emuladores sean compatibles**.

## Cómo compilar desde GitHub

1. Ve a la pestaña **Actions**.
2. Abre **LNR Windows CHD-to-PBP builds**.
3. Selecciona **Run workflow**, rama `lnr/windows-chd-build`.
4. Cuando termine correctamente, descarga el artefacto de la compilación
   **PSXPackagerLNR-v...-b...-Windows-x64**, que contiene los tres ZIP.

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

## Estado de la edición

La GUI LNR permite elegir discos separados/unidos, nivel 3 de compresión
predeterminado, conversión directa CHD→PBP, vista previa de juegos,
progreso y registro, y una Suite de discos experimental con modos
PBP/CHD/BIN-CUE/ISO.

Las funciones de borrado de CHD y apagado automático son opcionales y
**siempre están desactivadas por defecto**. Antes de publicar una versión
estable deben completarse pruebas reales de las conversiones adicionales
y de estas funciones avanzadas.

## Licencia y créditos

Respetar `LICENSE` y todos los créditos del proyecto original. La adaptación y automatización de compilación LNR no implican autoría del software de origen.
