# PSXPackagerLNR — compilación Windows y ejecutables

Este repositorio es un fork de [RupertAvery/PSXPackager](https://github.com/RupertAvery/PSXPackager). LaNaveRETRO no creó el motor original. Se conserva la licencia MIT y la autoría original.

## Compilación Windows x64 y distribución

GitHub Actions genera tres ZIP con versión y número de compilación:

- **GUI-win-x64.zip** (recomendado): incorpora **`PSXPackagerLNR.exe`**,
  que abre directamente el conversor CHD→PBP por lotes; y
  **`PSXPackagerGUI.exe`**, para acceder opcionalmente a la
  interfaz clásica original de PSXPackager. Ambos ejecutables
  comparten el mismo código de GUI y las mismas herramientas.
- **CLI-win-x64.zip**: programa de línea de comandos `psxpackager.exe`.
- **Toolkit-win-x64.zip**: scripts portables BAT y PowerShell.

La compilación GUI incluye además el motor `psxpackager.exe`,
la base de datos, créditos, licencia y manuales. **No borres ni
muevas archivos individuales**: extrae todo el ZIP y ejecuta
`PSXPackagerLNR.exe`. Ambas interfaces usan el mismo motor y
la conversión CHD→PBP no necesita CHDMAN.

La compilación se realiza con .NET 8 **autocontenido y SingleFile**
en Windows x64. El proceso publica un único paquete de la GUI y
genera dos nombres de ejecutable a partir de él: el código de
arranque comprueba el nombre del proceso y abre la ventana apropiada.
No se mantiene una segunda versión del motor ni se necesita instalar
.NET en el equipo de destino.

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
