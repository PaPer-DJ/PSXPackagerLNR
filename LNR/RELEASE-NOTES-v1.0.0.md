# PSXPackagerLNR v1.0.0 — primera versión estable del fork LaNaveRETRO

**PSXPackagerLNR** es la edición y automatización de **PaPer-DJ / LaNaveRETRO** basada en el software libre [PSXPackager de RupertAvery y colaboradores](https://github.com/RupertAvery/PSXPackager). **No hemos creado el motor original**, que conserva sus créditos y licencia MIT. Esta es una **release estable del fork**, no una actualización ni publicación en el repositorio de los autores originales.

**Windows x64 · Portátil · .NET 8 autocontenido · Sin instalador**.

## Descargar e instalar

Descarga **GUI-win-x64.zip** (recomendado), extráelo **completo** y abre el acceso directo portátil **`#PSXPackagerLNR.lnk`** en la misma carpeta. No debes mover ese archivo fuera del directorio junto a la GUI y sus recursos.

También se publican el **CLI-win-x64.zip** y el **Toolkit-win-x64.zip**, con **SHA256SUMS.txt** para verificar los ZIP. No hay necesidad de instalar .NET en Windows x64 para utilizar los paquetes autocontenidos.

## CHD → PBP directo para PlayStation 1

- Procesamiento **directo y por lotes** desde CHD a PBP sin extraer BIN/CUE intermedios y sin instalar CHDMAN.
- Conversión multidisco separada o unida usando M3U, con **nivel 3 de compresión por defecto** (también disponibles 1, 5 y 9).
- Procesos independientes simultáneos (**1, 2, 3, 4, 6, 8 o Auto**) y **Pausar / Pausando / Reanudar** sin interrumpir juegos activos.
- Explorador de carpetas propio con **Atrás, Entrar, ↑/↓, Enter, Retroceso** y opción de analizar subcarpetas, preservando su organización en la carpeta PBP.
- Progreso, fases, tamaños de origen y destino, variaciones, tiempos, ETA orientativa y registro de actividad.
- No sobrescribe PBP existentes. Por defecto **no borra CHD originales**. La eliminación opcional de originales, **únicamente en la GUI principal**, requiere activación y doble confirmación, y solo se intenta después de validar el nuevo PBP. Conserva copias de seguridad; una cabecera correcta no garantiza compatibilidad en la consola.

## Toolkit BAT / PowerShell

- Menú con instrucciones previas para elegir modo multidisco, nivel y simultaneidad.
- **1 - SEPARADOS [RECOMENDADO]**; también disponible modo Unido para M3U.
- Controles de teclado **P = Pausar de forma segura**, **R = Reanudar**, **C = Cancelar**.
- Conserva siempre CHD, M3U y SBI originales. No necesita CHDMAN para CHD → PBP directo.

## CLI del fork

El ZIP CLI incluye el nuevo documento **`LEEME-CLI-LNR.txt`**, que distingue claramente el motor original de los cambios LNR:

- Compresión predeterminada **nivel 3** (upstream: nivel 5).
- Salida de progreso utilizable con stdout redirigido (`Progress: NN%`) y compatibilidad con ejecución sin consola visible.
- Binario Windows x64 autocontenido compilado para .NET 8.
- La concurrencia y la pausa pertenecen a GUI/Toolkit; no son funciones añadidas al algoritmo interno del CLI.

## Suite de discos — PS1, Saturn, PS2 y Dreamcast

Se incluyen **siete conversiones** y un modo de **verificación GD-ROM**:

| Función | Observaciones |
|---|---|
| PBP → CHD | PS1, un CHD por disco; multidisco nombrado `Juego (Disc 1).chd`, etc.; utiliza CHDMAN y verifica resultados |
| PBP → BIN/CUE | PS1, conserva las pistas disponibles y referencias CUE; multidisco con `(Disc N)` |
| PBP → ISO **[ No Recomendado ]** | Solo un disco con una pista de datos MODE1. Rechaza MODE2, audio, XA y multidisco; no es preservación íntegra de CD |
| CHD → BIN/CUE | CD de PS1, Saturn y PS2 CD; requiere CHDMAN |
| CHD → ISO | DVD PS2; requiere CHDMAN |
| **Dreamcast GDI → CHD** | Comprueba descriptor y pistas, crea CHD con CHDMAN y ejecuta verify antes de publicarlo |
| **Dreamcast CHD → GDI** | Solo CHD con metadatos GD-ROM CHGD/CHGT; extrae GDI y todas sus pistas a una carpeta, validándolas |
| **Dreamcast · Verificar CHD** | Solo lectura: `chdman info` y `chdman verify`; no crea archivos ni modifica el original |

La Suite permite **1/2/3/4/6/8/Auto conversiones simultáneas**, pausa segura al finalizar todos los trabajos activos, cancelación de procesos, búsqueda de subcarpetas desde «Ver carpetas…», progreso por archivo, ETA y estadísticas. **Nunca elimina los PBP, CHD, GDI o pistas originales.**

**CHDMAN no se redistribuye**: debes proporcionar una copia legítima de la herramienta de MAME para las operaciones de Suite que la necesitan.

### Precauciones para Dreamcast y preservación

La conversión **GDI ↔ CHD** se ha preparado con pruebas sintéticas automáticas de estructura GDI, pistas, metadatos GD-ROM y límites de seguridad. **Todavía debe validarse con imágenes Dreamcast reales** y una versión concreta de CHDMAN. La extracción de un CHD a GDI no garantiza recuperar exactamente nombres, pregaps o hashes de un volcado anterior; no puede reconstruir información ausente en el origen. **CDI no está admitido** y no se debe confundir con un GD-ROM original.

## Pruebas y alcance de esta v1.0.0

La aplicación principal, el Toolkit, los controles y la navegación de las betas anteriores ya han sido comprobados por el autor en Windows. La compilación de v1.0.0 supera pruebas automatizadas de Windows, restauración del acceso directo portátil, seguridad de los archivos, conversiones y comprobaciones de datos sintéticos.

**Estable significa la primera versión pública normal del fork**, no que se garantice compatibilidad universal con todos los discos, todas las versiones de CHDMAN o todos los sistemas. Conservar el material de origen es especialmente importante antes de utilizar las nuevas funciones Dreamcast.

## Créditos e independencia del fork

- Motor y proyecto original: [RupertAvery/PSXPackager](https://github.com/RupertAvery/PSXPackager) y colaboradores, con licencia MIT.
- Fork, interfaces, Toolkit y mejoras LNR: [PaPer-DJ/PSXPackagerLNR](https://github.com/PaPer-DJ/PSXPackagerLNR) — LaNaveRETRO.
- **Esta release y su etiqueta se crean solo en el fork**. No cambian el código, las versiones, las releases ni los permisos del proyecto original.
- Las betas públicas del fork [beta.31](https://github.com/PaPer-DJ/PSXPackagerLNR/releases/tag/v0.1.0-beta.31), [beta.30](https://github.com/PaPer-DJ/PSXPackagerLNR/releases/tag/v0.1.0-beta.30) y [beta.29](https://github.com/PaPer-DJ/PSXPackagerLNR/releases/tag/v0.1.0-beta.29) siguen disponibles sin cambios.

Consulta el [README](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/README.md), el [manual en español](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/LNR/GUI-AYUDA.md) y el [historial de cambios](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/CHANGELOG-LNR.md).
