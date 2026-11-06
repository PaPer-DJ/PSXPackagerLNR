# Historial de versiones de PSXPackagerLNR

Este historial se refiere **solo a las adaptaciones LNR** sobre el proyecto
original [PSXPackager](https://github.com/RupertAvery/PSXPackager).
La autoria original y la licencia MIT se conservan.

## v0.1.0-beta.1 — 6 noviembre 2026 (version de pruebas)

Primera version numerada del fork LNR. No es una release estable.

- Compilaciones Windows x64 autocontenidas de GUI y CLI.
- Conversion directa CHD a PBP, usando el lector del codigo original.
- Herramienta LNR por lotes integrada en la interfaz grafica.
- Multidisco separado / unido mediante M3U y deteccion de series.
- Compresion 1, 5 o 9 (predeterminada 5).
- Nombres originales, carpeta PBP, sin sobrescribir ni borrar CHD.
- Registro en castellano, estadisticas y cancelacion.
- Correccion del fallo `ConsolePal.set_CursorVisible` al usar la CLI desde GUI.
- Progreso por fase leido en tiempo real de la CLI y mostrado por archivo.
- Panel de actividad ampliable, indicadores de exito/error/saltado.
- Se evita el aviso original de primera ejecucion para el formato PSP.

### Pendiente antes de v1.0.0

- Validar GUI, multidisco M3U y pistas de CD-Audio en equipos reales.
- Revisar opciones de ficheros SBI y compatibilidad de emuladores.
- Completar traduccion/identidad de la aplicacion.

### Regla para futuras versiones

- Cambios experimentales/fallos corregidos: avanzar beta.1, beta.2, beta.3…
- Candidata estable: 1.0.0-rc.1 (cuando corresponda).
- Publicacion estable: 1.0.0 tras pruebas.
- Cada ejecucion de GitHub Actions recibe además su numero de compilacion (bNN).
  Incluso si la version funcional no cambia, **cada ZIP tendrá un nombre unico**.
- El archivo `LNR/VERSION` es la unica fuente de la version de producto;
  no alteramos artificialmente la numeracion original de PSXPackager.

**Descarga:** usar el artefacto de GitHub Actions correspondiente a la version
y compilacion deseadas; los artefactos temporales caducan. Las versiones
publicas definitivas deberan archivarse en GitHub Releases.
