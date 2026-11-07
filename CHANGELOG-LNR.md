# Historial de versiones de PSXPackagerLNR

Este historial se refiere **solo a las adaptaciones LNR** sobre el proyecto
original [PSXPackager](https://github.com/RupertAvery/PSXPackager).
La autoria original y la licencia MIT se conservan.

## v0.1.0-beta.4 — 7 noviembre 2026 (marca LNR y avance global)

- Logo real **LNR MOD**, aportado para esta edición, optimizado y
  guardado como recurso gráfico propio del fork. Se muestra pequeño
  en la cabecera de CHD→PBP para no restar espacio a los juegos.
- Nueva ventana **Acerca de LNR** con el logo más visible, número de
  versión, atribución al programa original, referencia a la licencia MIT
  y enlaces a los repositorios original y LNR. El icono y las
  funciones originales no se sustituyen.
- Porcentaje **global y exacto de archivos procesados** en la barra
  inferior, independiente del porcentaje de cada fase del motor CLI.
- **Tiempo restante orientativo** desde que finaliza la primera
  conversión válida: usa segundos por byte observado y los tamaños
  reales de los CHD que quedan por convertir.
- El tiempo restante se refresca durante la conversión una vez por
  segundo. Puede variar considerablemente por pistas de audio,
  número de discos, complejidad y velocidad de almacenamiento.
  Cuando un juego tarda bastante más que lo esperado se muestra
  «recalculando», no un falso 0 s.
- Si se refresca el análisis se reinician los cálculos del lote.
  Los archivos preexistentes no cuentan como procesados hasta que
  el lote realmente los salta.
- El código original de PSXPackager y la licencia MIT permanecen
  intactos; `master` no se ha fusionado.

**Por comprobar en Windows:** legibilidad del logo, ventana de créditos,
porcentaje global y variación de la estimación con juegos de tamaños
distintos. No es aún una versión estable.

## v0.1.0-beta.3 — 7 noviembre 2026 (estadísticas por juego)

- Eliminado el porcentaje duplicado del estado: el texto muestra la
  fase actual (por ejemplo, «Escribiendo PBP») y el porcentaje se muestra
  **una sola vez**, junto a su barra gráfica.
- Nuevas columnas **CHD, PBP, Variación y Tiempo** en la tabla. CHD suma
  los discos en modo multidisco; el PBP se mide al finalizar o, si
  ya existía, al analizar la carpeta.
- La variación está expresada como **PBP menos CHD**: signo positivo
  (rojo) cuando el PBP crece, negativo (verde) cuando ocupa menos.
  El porcentaje relativo aparece como ayuda al pasar el cursor.
- Cronómetro por archivo para identificar conversiones lentas.
  La duración permanece en la fila tras completar o fallar.
- El resumen del lote informa la variación con el mismo criterio
  que la tabla, sin presentar como «ahorro» un incremento de tamaño.
- Un fallo al copiar un archivo SBI auxiliar se comunica como aviso
  sin deshacer el éxito de un PBP correctamente convertido.
- Ajustados los anchos de columnas y recortados los nombres largos
  visualmente, mostrando el nombre completo al pasar el cursor.

**Pendiente de validación manual en Windows**: comprobar anchuras,
colores, medidas y tiempos en archivos CHD reales. No es una release estable.

## v0.1.0-beta.2 — 7 noviembre 2026 (interfaz pulida)

- Nuevo **explorador de carpetas con vista previa CHD**: muestra las
  subcarpetas y los nombres/tamaños de los juegos antes de seleccionar
  la carpeta, sin obligar a escoger un CHD individual.
- **Análisis automático** al seleccionar la carpeta, recuperar la última
  carpeta utilizada, pegar una ruta y pulsar Intro, o cambiar el modo
  multidisco. El botón **Analizar** sigue disponible para refrescar la lista.
- La ventana CHD → PBP recuerda **última carpeta, modo multidisco,
  nivel de compresión, tamaño de ventana, altura del registro y autoscroll**.
  Preferencias locales en `%LOCALAPPDATA%\\PSXPackagerLNR\\gui-preferencias.json`,
  sin modificar la configuración del PSXPackager original.
- Botones **Abrir carpeta PBP** y **Copiar registro**; seguimiento
  automático del registro activable/desactivable para poder leer mensajes
  anteriores durante conversiones largas.
- Avance de cada juego etiquetado por **fase real** que notifica la CLI
  (conversión, escritura, extracción o descompresión). **No confundir el
  porcentaje de cada fase con el progreso global del juego**.
- Al analizar, identifica de antemano los PBP existentes y los marca como
  **Saltado / Ya existe**; nunca sobrescribe el archivo existente.
- Revisión de textos de confirmación, ayudas contextuales y atajos de la
  barra original en castellano, sin alterar las funciones de base.
- Mantiene el registro redimensionable, los indicadores por archivo, el
  destino independiente PBP y la conservación de los CHD.

**Pruebas requeridas:** comprobar en Windows la persistencia de preferencias,
el botón de abrir carpeta, el estado de archivos preexistentes, la cancelación
y el indicador de fase mientras convierte CHD reales. La conversión multidisco
unida y los ficheros SBI siguen pendientes de pruebas de compatibilidad.

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
