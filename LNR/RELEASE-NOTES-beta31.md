# PSXPackagerLNR v0.1.0-beta.31 — Toolkit paralelo, pausa y explorador de carpetas

**Nueva beta pública del fork PaPer-DJ / LaNaveRETRO**, basado en [PSXPackager de RupertAvery y colaboradores](https://github.com/RupertAvery/PSXPackager). La adaptación LNR no se atribuye la autoría del motor original y conserva su licencia MIT.

**Windows x64 · portátil y autocontenido (.NET 8) · sin instalador.** El autor ha validado personalmente esta beta en Windows; las comprobaciones de GitHub Actions se han superado. Es una **pre-release**, no una versión estable.

## 1. Toolkit CHD → PBP: conversiones simultáneas y controles en directo

El Toolkit con menú BAT y motor PowerShell 5.1 permite procesar **varios juegos de PS1 CHD → PBP simultáneamente** usando procesos `psxpackager.exe` independientes; no se comprime un mismo juego con varios hilos. Un juego multidisco se sigue tratando como una única tarea.

- Selector **1, 2, 3, 4, 6, 8 o 0/Auto**; por defecto **4 (recomendado HDD)**, con **8 sugerido para SSD** y 0/Auto limitado a 1–4 procesos según la CPU.
- **P — Pausar:** no inicia más juegos, pero deja que los procesos en marcha terminen, validen sus PBP y limpien sus carpetas temporales. Muestra `PAUSANDO` y después `EN PAUSA` cuando no quedan trabajos activos. No congela ni mata un PBP a mitad de escritura.
- **R — Reanudar:** continúa los trabajos pendientes sin repetir los completados.
- **C — Cancelar:** detiene las conversiones activas, limpia sus archivos temporales incompletos y sale del lote. Los PBP ya finalizados se conservan. Es una acción diferente de Pausar.
- Las teclas **P/R/C** se usan en la ventana de consola durante la ejecución, **sin pulsar Intro**. El BAT muestra una explicación de uso, rutas y reglas de seguridad **antes** de preguntar las opciones.

**Seguridad:** el Toolkit nunca elimina CHD, M3U ni SBI originales; no sobrescribe PBP existentes; valida una cabecera PBP reconocible antes de trasladar el resultado; mantiene temporales separados por juego, copia SBI auxiliares cuando corresponde y genera informes en `LOGS`. La compatibilidad de PBP multidisco y SBI externo depende del emulador.

**Rendimiento:** con cuatro conversiones paralelas en la GUI beta.30, el autor había observado una mejora de velocidad cercana a 4× en su equipo. No es una garantía de rendimiento para todos los procesadores o discos; la recomendación 8 para SSD no equivale a una aceleración medida de 8×.

## 2. GUI LNR: navegador de carpetas completamente mejorado

- Botones reorganizados: **Atrás** a la izquierda y **Entrar** a la derecha.
- **Selecciona una subcarpeta + Entrar**, o **doble clic**, para acceder. También puedes escribir o pegar una ruta manual y pulsar Entrar.
- **↑ / ↓** recorren las subcarpetas; **Enter** abre la seleccionada.
- **Atrás** vuelve al directorio superior dejando **seleccionada y enfocada la carpeta de la que acabas de salir**; puedes volver a entrar inmediatamente sin utilizar el ratón.
- **Retroceso (Backspace)** también vuelve al directorio superior, excepto si estás editando una ruta en el cuadro de texto: allí Retroceso sigue borrando caracteres.
- Ventana algo más ancha, textos de ayuda y casilla **«También subcarpetas»** en la parte inferior, desactivada por defecto.

### «También subcarpetas»: análisis y salida segura

Si marcas la casilla, la GUI busca CHD también dentro de subcarpetas y muestra las rutas relativas en la vista previa. **Conserva la estructura de directorios en PBP**: `CHD/Acción/Juego.chd` → `CHD/PBP/Acción/Juego.pbp`. Así se evitan colisiones entre títulos con el mismo nombre en distintas carpetas.

Los juegos multidisco y listas M3U se agrupan **solo dentro de su carpeta de origen**. Las carpetas `PBP`, `.tools`, temporales y enlaces/junciones de directorios se omiten en la búsqueda. La opción desmarcada mantiene el análisis original de una sola carpeta. Los PBP existentes no se sobrescriben.

## 3. Funciones anteriores que se conservan

- CHD → PBP directo en lote, sin generar BIN/CUE intermedios ni necesitar CHDMAN en esta ruta de conversión.
- Compresión PBP **nivel 3 por defecto**, con niveles 1/5/9 disponibles.
- GUI: 1, 2, 3, 4, 6, 8 o Auto conversiones simultáneas, pausa segura entre juegos, reanudación, cancelación, progreso, tamaños, informes y estimación aproximada.
- Modo multidisco (M3U), copia de SBI, comportamiento conservador de destinos.
- **Borrado de CHD opcional en GUI, desactivado por defecto, irreversible y con doble confirmación**, únicamente después de comprobar básicamente el PBP generado. La comprobación de cabecera no garantiza que el juego arranque en una consola.
- Suite CD / CHD / ISO (algunas operaciones requieren herramientas CHDMAN aportadas por el usuario).
- Acceso directo portátil nativo **`#PSXPackagerLNR.lnk`** aprobado por el autor, sin nuevo ejecutable lanzador.

## Paquetes Windows x64

- **GUI-win-x64.zip — recomendado:** interfaz principal y clásica, CLI incluida, Suite y acceso directo portátil.
- **CLI-win-x64.zip:** conversor por línea de comandos.
- **Toolkit-win-x64.zip:** BAT, PowerShell, motor CLI y documentación; no requiere abrir la GUI.
- **SHA256SUMS.txt:** hashes verificables de los tres ZIP.

Para usar la GUI, extrae **todo** el ZIP y abre `#PSXPackagerLNR.lnk` **desde su carpeta original**, sin separarlo del ejecutable ni de los recursos. Para usar el Toolkit, extrae su ZIP dentro de la carpeta de CHD o arrastra una carpeta CHD a su BAT.

**Compatibilidad y precauciones:** beta validada por el autor en Windows y comprobada mediante pruebas sintéticas del workflow; no todas las combinaciones de almacenamiento, archivos CHD, discos, emuladores o sistemas Windows están probadas. Conserva copias de seguridad de tu colección.

Las betas públicas anteriores [beta.30](https://github.com/PaPer-DJ/PSXPackagerLNR/releases/tag/v0.1.0-beta.30) y [beta.29](https://github.com/PaPer-DJ/PSXPackagerLNR/releases/tag/v0.1.0-beta.29) se mantienen intactas. **No se modifica `master`, ni el motor original ni el acceso directo aprobado**.

**Créditos:** [RupertAvery / PSXPackager original](https://github.com/RupertAvery/PSXPackager) y colaboradores; fork y herramientas LNR por **PaPer-DJ / LaNaveRETRO**. Código y licencia MIT según `LICENSE`.

[Repositorio PSXPackagerLNR](https://github.com/PaPer-DJ/PSXPackagerLNR) · [Manual GUI](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/LNR/GUI-AYUDA.md) · [Manual Toolkit](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/LNR/Batch/README.md) · [Historial de cambios](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/CHANGELOG-LNR.md)
