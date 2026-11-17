# PSXPackagerLNR v0.1.0-beta.30 — conversiones simultáneas y pausa segura

**Segunda beta pública del fork PaPer-DJ / LaNaveRETRO**, basado en el [PSXPackager original de RupertAvery y colaboradores](https://github.com/RupertAvery/PSXPackager). **Windows x64 · portátil · .NET 8 autocontenido · sin instalación.** Se conserva la licencia MIT y la atribución original: el motor no es una creación desde cero de LaNaveRETRO.

## Novedad principal: CHD → PBP con varias conversiones simultáneas

La interfaz propia LNR ahora permite convertir **varios juegos de PS1 desde CHD a PBP al mismo tiempo**. En vez de comprimir los discos de un juego en paralelo, se ejecutan **procesos CLI independientes por juego**: cada proceso escribe en su propia carpeta temporal. Los PBP multidisco siguen tratándose como **una sola tarea**.

- Selector **Conversiones simultáneas** con **1, 2, 3, 4, 6, 8 y Auto**.
- **4 — Recomendado (HDD)**: valor inicial de la interfaz.
- **8 — Recomendado (SSD)**: para probar en equipos con SSD y recursos suficientes.
- **1 — Secuencial**: conserva la modalidad de procesamiento de beta.29.
- **Auto**: selecciona entre 1 y 4 procesos según el procesador lógico del equipo; no implica detectar el tipo de disco.
- Los selectores **Multidisco, Compresión y Conversiones simultáneas** están en **la misma fila**, sin avisos laterales permanentes. La ayuda contextual explica el coste en CPU, RAM y espacio temporal.

### Pruebas reales de LaNaveRETRO: rendimiento observado

El autor del fork ha **probado personalmente la beta.30 en Windows** y confirma que las conversiones en paralelo y la nueva pausa/reanudación funcionan según lo esperado. Con **4 conversiones simultáneas**, observó que la velocidad global de procesamiento **prácticamente se cuadruplicaba** respecto al modo secuencial, sin saturación apreciable de la CPU ni del SSD.

En el Administrador de tareas de Windows observó, aproximadamente, **17 MB/s de escritura** y **10,5 MB/s de lectura por conversión** durante esas pruebas.

**Los resultados corresponden a su equipo y juegos: no representan un benchmark general ni garantizan ganancias idénticas en todos los sistemas.** La recomendación de **8 para SSD** surge del margen de recursos apreciado con 4; **no se ha documentado una medición que demuestre ocho veces más rendimiento con 8 procesos**. Los HDD pueden acusar operaciones simultáneas de búsqueda y en algunos equipos será preferible usar 1 o 2.

## Nuevo botón contextual: Convertir → Pausar → Reanudar

El botón principal cambia de función e icono a lo largo del proceso:

1. **Convertir ▶**: inicia el lote, tras confirmar los ajustes.
2. **Pausar ‖**: deja de iniciar nuevos juegos, pero **no interrumpe ni suspende los procesos que ya están creando PBP**.
3. **Pausando… (reloj)**: espera a que terminen normalmente todos los juegos activos, incluida validación del PBP, archivos SBI y posibles acciones sobre CHD expresamente autorizadas.
4. **EN PAUSA**: cuando ya no hay trabajos activos, la interfaz indica el estado y ofrece **Reanudar ▶**.
5. **Reanudar ▶**: continúa con las tareas pendientes sin repetir las que terminaron. La pausa no se incluye en el tiempo activo del lote.

**Cancelar sigue siendo distinto de Pausar**: termina el lote y detiene los procesos activos; funciona incluso mientras se espera una reanudación. **La pausa puede tardar** si algún juego es grande: se realiza de forma segura **entre juegos**, no a mitad de un PBP.

## Mejoras conservadas de beta.29

- **CHD → PBP directo y por lotes**, sin CHDMAN ni BIN/CUE intermedios en el flujo principal.
- **Nivel 3 de compresión predeterminado LNR**, conservando opciones 1, 5 y 9. Basado en pruebas prácticas de tamaño y carga en PSP original; no se prometen ganancias universales.
- PBP separados y multidisco con M3U, seguimiento del juego activo, progreso por fase, estimación global, tamaños CHD/PBP y diferencias positivas o negativas.
- Sin sobrescribir PBP existentes. Los CHD originales **se conservan por defecto**. El borrado es **opcional, irreversible y requiere doble confirmación**; solo después de crear y comprobar básicamente el PBP. La validación de cabecera no garantiza compatibilidad en un emulador.
- Apagado opcional al finalizar el lote con cuenta atrás cancelable y salvaguardas.
- Suite adicional de cinco rutas para PS1, Sega Saturn CD y PS2 CD/DVD, con **CHDMAN externo suministrado por el usuario** donde sea necesario. **No convierte juegos de Saturn o PS2 a PBP de PS1.**
- Acceso directo portátil nativo **`#PSXPackagerLNR.lnk`** aprobado por el autor; **sin un segundo EXE lanzador**. Conservar junto a `PSXPackagerGUI.exe` en la carpeta descomprimida.

## Descargas

- **GUI-win-x64.zip — recomendado:** interfaz clásica y LNR por lotes, CLI integrada, Suite y acceso directo.
- **CLI-win-x64.zip:** línea de comandos.
- **Toolkit-win-x64.zip:** herramientas BAT/PowerShell.
- **SHA256SUMS.txt:** valores SHA-256 verificables para los tres ZIP.

**Para instalar:** extraer el ZIP GUI completo y abrir `#PSXPackagerLNR.lnk`. El paquete **es autocontenido**; no es necesario instalar .NET por separado. No mover el acceso directo fuera de su carpeta.

## Estado y advertencias

**Beta pública (pre-release), no versión estable.** LaNaveRETRO ha confirmado en Windows el funcionamiento de la ejecución paralela y de la pausa segura; CI ha compilado y comprobado el empaquetado, el acceso directo portátil y pruebas de regresión. No están probadas todas las combinaciones de juegos, HDD/SSD, códecs, formatos y emuladores. No utilizar el borrado irreversible sin copia de seguridad y pruebas de los PBP.

**Esta publicación no modifica `master`, el motor del software original ni la etiqueta y archivos de la [beta.29 anterior](https://github.com/PaPer-DJ/PSXPackagerLNR/releases/tag/v0.1.0-beta.29).**

## Créditos

Proyecto original: [RupertAvery/PSXPackager](https://github.com/RupertAvery/PSXPackager), colaboradores y titulares del copyright indicado en `LICENSE` (MIT). Fork, automatización, interfaz CHD→PBP por lotes y mejoras LNR: **PaPer-DJ / LaNaveRETRO**.

[Repositorio LNR](https://github.com/PaPer-DJ/PSXPackagerLNR) · [README y documentación](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/README.md) · [CHANGELOG-LNR.md](https://github.com/PaPer-DJ/PSXPackagerLNR/blob/lnr/windows-chd-build/CHANGELOG-LNR.md)
