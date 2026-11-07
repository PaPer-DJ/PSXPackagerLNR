# Historial de versiones de PSXPackagerLNR

Este historial se refiere **solo a las adaptaciones LNR** sobre el proyecto
original [PSXPackager](https://github.com/RupertAvery/PSXPackager).
La autoria original y la licencia MIT se conservan.

## v0.1.0-beta.18 — 7 noviembre 2026 (acceso directo sin ejecutable lanzador)

- El paquete GUI Windows x64 sustituye el ejecutable auxiliar `PSXPackagerLNR.exe` por `PSXPackagerLNR - Conversion por lotes.lnk`.
- El acceso directo abre `PSXPackagerGUI.exe --lnr-batch`, usando el enrutamiento que ya existía en beta.17 y mostrando directamente la ventana LNR de conversión por lotes.
- `PSXPackagerGUI.exe` sin argumentos mantiene la interfaz clásica. `psxpackager.exe` sigue incluido como motor de conversión.
- Se elimina la compilación MSVC del lanzador Win32 y se añade comprobación de creación y contenido del acceso directo en GitHub Actions.
- Los accesos directos de Windows pueden depender de su ubicación: se recomienda extraer el paquete completo y verificar el enlace después de desplazar la carpeta.
- No se modifican las operaciones de conversión, borrado, compresión ni los valores guardados por el usuario.

**Estado:** beta de desarrollo; probar arranque del enlace y funcionamiento después de mover la carpeta en Windows.

## v0.1.0-beta.17 — 7 noviembre 2026 (lanzador ligero sin duplicación)

- **PSXPackagerLNR.exe ya no es una copia completa de PSXPackagerGUI.exe.**
  Se sustituye por un ejecutable **nativo Win32 muy pequeño**, compilado
  con MSVC e icono del programa original, sin .NET incrustado.
- El lanzador abre `PSXPackagerGUI.exe --lnr-batch` utilizando Windows;
  la GUI ejecuta directamente la ventana LNR de conversión por lotes.
  Al abrir `PSXPackagerGUI.exe` sin argumentos sigue apareciendo la
  interfaz clásica de PSXPackager.
- **Única copia de GUI autocontenida** en el paquete: ya no se duplica
  un EXE grande y se reduce el tamaño total de la distribución ZIP.
  La CLI `psxpackager.exe` sigue incluida para convertir CHD a PBP.
- GitHub Actions compila el lanzador nativo, verifica que ocupa
  **menos de 1 MB**, lo incorpora al ZIP y comprueba que la aplicación
  completa, la CLI y los recursos necesarios siguen presentes.
- Se conservan créditos, licencia MIT y función original de conversión;
  manual en castellano y guía de compilación actualizados.
- El lanzador pequeño **necesita estar en la misma carpeta** que la
  GUI completa: no es un conversor independiente ni debe aislarse.

## v0.1.0-beta.16 — 7 noviembre 2026 (arranque directo y ahorro de espacio)

- Nuevo **`PSXPackagerLNR.exe`** en el paquete GUI Windows x64:
  **abre directamente el conversor LNR CHD→PBP por lotes**, sin
  pasar por la pantalla inicial de PSXPackager.
- Se conserva también **`PSXPackagerGUI.exe`** para quien quiera
  utilizar las funciones de la GUI clásica. Ambos ejecutables
  gráficos comparten exactamente el mismo código y el motor CLI.
- El paquete GUI sigue incluyendo **`psxpackager.exe`**, necesario
  para la conversión directa sin CHDMAN; no debe eliminarse.
- Selección de interfaz mediante el nombre del ejecutable dentro del
  arranque WPF; `PSXPackagerLNR.exe` y `PSXPackagerGUI.exe`
  son copias del mismo ejecutable autocontenido, sin duplicar
  ni modificar los motores internos.
- GitHub Actions comprueba la presencia de los dos EXE y del
  motor CLI **dentro del ZIP**, además de verificar la selección
  de la ventana principal.
- La sección **Eliminar CHD convertidos** comienza ahora
  explicando la razón de su existencia: convertir colecciones
  grandes sin conservar simultáneamente todos los CHD y sus PBP,
  liberando espacio después de cada conversión verificada.
- Aclarado que es necesario espacio libre al menos para el PBP
  del trabajo actual; **si quieres conservar los CHD originales
  debes dejar la casilla desmarcada**.
- Documentada la posibilidad de **PBP→CHD** posterior con CHDMAN
  y su limitación: **no garantiza recuperar todos los datos
  perdidos ni una imagen idéntica al CHD original**.
- Manual castellano, ayuda integrada, README, tooltip y
  confirmación de borrado actualizados. No cambian las
  operaciones sobre disco ni los criterios de borrado.

**Estado:** rama de desarrollo; verificar el doble acceso gráfico
en Windows antes de publicar una versión estable.

## v0.1.0-beta.15 — 7 noviembre 2026 (anexo técnico para usuarios avanzados)

- Incorporado un **anexo técnico extenso al final de la información LNR
  en el README**, justo antes de la documentación del proyecto original,
  que permanece conservada y separada.
- Explicación de **DEFLATE: LZ77, Huffman, greedy/lazy matching, hash
  chains y límites de búsqueda**, con la tabla de parámetros
  `GOOD_LENGTH`, `MAX_LAZY`, `NICE_LENGTH` y `MAX_CHAIN`
  **para todos los niveles 1 a 9** de SharpZipLib 1.4.2.
- Se presentan las **observaciones de LaNaveRETRO en PSP original**:
  pausas de carga más cortas para niveles 1–3 frente a 4–5 en algunos
  juegos, y ahorro adicional pequeño del 4 al 5 y del 5 al 9 en
  las imágenes comparadas.
- Se distingue **trabajo de compresión en PC** de **descompresión
  durante la ejecución en PSP**. No se afirma que el compresor
  ejecute búsquedas hash en PSP, ni se atribuye una reducción de
  CPU demostrada sin medidas específicas. Se justifica el nivel 3
  como **recomendación práctica**, no como garantía universal.
- Ventana **Acerca de PSXPackagerLNR**: discreta referencia y enlace
  clicable al análisis, al final de los créditos; contenido desplazable
  y tamaño ajustado para pantallas pequeñas.
- El manual y la presentación inicial del fork enlazan al anexo;
  créditos, licencia, algoritmo, niveles predeterminados y salidas
  de conversión no se modifican.

**Pendiente:** revisión visual del diálogo «Acerca de» en Windows.

## v0.1.0-beta.14 — 7 noviembre 2026 (nivel 3 por defecto en toda la suite y pruebas en PSP)

- **Nivel 3 predeterminado en todas las interfaces del fork**: GUI
  clásica de PSXPackager (pantalla Ajustes/Compression), GUI LNR por
  lotes, CLI `psxpackager.exe` y toolkit BAT/PowerShell.
  Anteriormente, la GUI clásica y la CLI todavía partían de 5.
- Se mantienen los valores configurados previamente por el usuario:
  el cambio de nivel inicial solo se aplica a nuevas instalaciones o
  instalaciones sin preferencias guardadas. Los niveles 1–9 siguen
  accesibles donde corresponde; no se elimina ningún nivel disponible.
- Se documentan pruebas prácticas de LaNaveRETRO con una **PSP
  original**: determinados juegos de PS1 convertidos con compresión
  1–3 tuvieron pausas de carga menores que con 4 o 5. En esas
  comparaciones, pasar del nivel 4 al 5 ahorró solo unos pocos MB,
  y del 5 al 9 aportó una reducción de tamaño muy pequeña.
  **Son observaciones de juegos concretos**, no una media estadística
  de todo el catálogo ni garantía universal.
- Separación explícita entre la estrategia greedy/lazy usada al
  **comprimir en PC** y el proceso de **descomprimir en PSP**;
  SharpZipLib 1.4.2 usa `DEFLATE_FAST` en niveles 1–4 y
  `DEFLATE_SLOW` en niveles 5–9. La experiencia en PSP es evidencia
  práctica independiente de la estrategia de creación del archivo.
- Página de compresión en GUI clásica, ayuda integrada, tooltips,
  README y manual del toolkit actualizados en castellano o inglés
  según la interfaz.
- Añadida comprobación de regresión en GitHub Actions para verificar
  el nivel predeterminado 3 en la CLI compilada y en las dos GUI.
- **No se modifica el repositorio original de RupertAvery** ni se
  atribuye a LaNaveRETRO la creación del motor PSXPackager.

## v0.1.0-beta.13 — 7 noviembre 2026 (explicación técnica de compresión nivel 3)

- Se mantiene el **nivel 3 predeterminado en la GUI LNR**; se mejora
  el texto del selector y la ayuda integrada con una explicación
  basada en el código real de SharpZipLib 1.4.2.
- **Verificado:** en esta biblioteca los niveles **1–4** usan
  `DEFLATE_FAST`, mientras **5–9** usan `DEFLATE_SLOW` para
  generar el PBP. Esto difiere del zlib clásico, cuyo umbral está
  entre 3 y 4.
- Recomendamos el 3 por el compromiso de **velocidad de compresión,
  tamaño y sencillez**, pero no afirmamos falsamente que la CPU de
  la consola tenga que repetir el cálculo de cadenas o búsquedas
  lazy/hash durante la descompresión.
- La documentación incluye fuentes y distingue explícitamente
  rendimiento de creación de PBP frente a reproducción/emulación.
- El motor original, formato PBP, niveles disponibles, preferencias
  existentes y datos de usuario no se han modificado.

## v0.1.0-beta.12 — 7 noviembre 2026 (orden de botones y enlaces para importar CHDMAN)

- **Limpiar registro** pasa a situarse **antes de Copiar registro**
  en Actividad y resultados; Abrir carpeta PBP permanece a la derecha.
- El aviso de **Importar ZIP...** de CHDMAN se sustituye por una
  ventana de confirmación con **enlaces clicables** a las páginas de
  Recalbox, OnionUI y las descargas oficiales de MAME.
- Los enlaces abren el navegador sin cerrar el cuadro, de modo que
  se puede descargar el ZIP antes de elegir **Sí, importar ZIP** o **No**.
- No se descarga, ejecuta ni instala software automáticamente al pulsar
  un enlace. El usuario sigue eligiendo expresamente el ZIP local.
- README y manual actualizados. Las conversiones y archivos existentes
  no sufren cambios.

## v0.1.0-beta.11 — 7 noviembre 2026 (limpiar registro de actividad)

- Añadido botón **Limpiar registro** inmediatamente a la derecha de
  **Copiar registro**, con el mismo espaciado y tamaño de la botonera.
- Limpia la zona visible **Actividad y resultados** y los mensajes
  acumulados en memoria desde la última limpieza. **Copiar registro**
  solo copia los mensajes posteriores a la limpieza.
- No interrumpe conversiones: los nuevos mensajes continúan apareciendo.
  No altera las estadísticas, CHD/PBP, archivos generados, ni informes
  TXT guardados previamente en disco.
- Ayuda integrada y manual en castellano actualizados.

## v0.1.0-beta.10 — 7 noviembre 2026 (mejor reparto del espacio y controles del lote)

- **Barra de progreso individual ampliada** hasta el porcentaje, usando todo
  el ancho sobrante de Estado/Progreso en lugar de encogerla.
- **Centrado** de cabeceras y valores en Discos, Origen, CHD, PBP,
  Variación, Tiempo y Estado/Progreso. Nombre PBP mantiene alineación
  a la izquierda. Las columnas numéricas adaptan su anchura según
  el contenido real, reservando el espacio restante al estado.
- **Tiempo total del lote:** al finalizar aparece «Lote Finalizado ·
  Tiempo total: mm:ss» (o hh:mm:ss); se distingue error y cancelación.
- Nuevo check **Apagar equipo al finalizar el lote**, siempre
  desactivado por defecto y con confirmación expresa adicional.
  Solo es elegible si se ha convertido al menos un juego y el lote
  no tiene errores, cancelaciones ni advertencias de eliminación.
- **Apagado seguro:** la aplicación muestra 60 segundos de cuenta atrás
  con botón para cancelarlo; cerrarla cancela el apagado. Después se
  solicita a Windows un apagado normal (sin forzar aplicaciones).
  Una tarea con documentos sin guardar puede bloquearlo o retrasarlo.
  Añadidas pruebas automáticas de la política de elegibilidad.
- **Mayor espacio inicial para la lista de CHD**: Actividad y resultados
  pasa de 300 a **200 píxeles** de alto, dejando más espacio para los
  juegos detectados. Sigue pudiendo redimensionarse arrastrando el
  separador. Se migra automáticamente el valor antiguo de 300 px
  cuando procede, sin sobrescribir otros tamaños personalizados.

**Pendiente de prueba visual y de flujo real en Windows.** No
se ha solicitado ninguna acción de apagado a un equipo durante
las pruebas de compilación.

## v0.1.0-beta.9 — 7 noviembre 2026 (pulido de botones y espaciado)

- Iconos vectoriales pequeños y nítidos en **Analizar** (lupa azul),
  **Convertir** (Play verde), **Ayuda y opciones** (información) y
  **Suite CD / CHD / ISO** (flechas verdes de conversión).
- Aplicado el mismo estilo también a los botones de analizar, convertir
  y ayuda de la suite de discos.
- Espaciado uniforme de **8 píxeles** entre Analizar/Convertir y
  Ayuda y opciones/Suite CD/CHD/ISO. Corrige el problema de botones
  inferiores visualmente pegados.
- Selector **Multidisco** estrechado de 185 a 150 píxeles, sin modificar
  la descripción ni las opciones Separados (1:1) / Unidos (M3U).
- Sin cambios en el motor de conversiones, algoritmos, archivos de
  origen, ni compatibilidad de formatos.

**Pendiente de prueba visual:** comprobar tamaño de los iconos y el
espaciado en Windows con escalado de pantalla 100 %, 125 % y superior.

## v0.1.0-beta.8 — 7 noviembre 2026 (PBP a BIN/CUE e ISO de datos)

- Añadida **PBP → BIN/CUE** por lotes, para extraer juegos de PS1
  sin CHDMAN. Los PBP multidisco producen varios CUE/BIN en una carpeta
  por juego y se mantienen las pistas de CD disponibles.
- Añadida **PBP → ISO de datos** para un caso técnicamente limitado:
  únicamente un CD con una sola pista de datos de 2048 bytes
  (MODE1/2048, MODE1/2352 o MODE2 Form1/2352).
- El conversor a ISO **rechaza** PBP multidisco, CD-Audio, CD-XA
  Form2, pistas múltiples, pausas y formatos no compatibles.
  No crea ISOs parciales a sabiendas: recomienda usar BIN/CUE.
  ISO NO es equivalente a una imagen de CD completo de PS1 ni sirve
  para conservar todas las pistas de audio o subcanales.
- Las conversiones PBP→BIN/CUE e ISO usan el lector del proyecto
  original y no necesitan CHDMAN; los otros tres modos mantienen
  su requisito de CHDMAN.
- Se conservan siempre los originales y no se sobrescriben archivos
  de destino existentes. Las extracciones provisionales se hacen
  en carpetas temporales que se limpian tras éxito o error.
- Añadidas **pruebas automáticas** de extracción ISO con sectores
  MODE1 y MODE2 Form1 sintéticos, y rechazo de pistas de audio,
  Form2 y CUE multidisco/multipista.

**Pendiente:** validar extracción real de PBP a BIN/CUE y el caso
ISO de datos en Windows. La suite sigue en pruebas beta.

## v0.1.0-beta.7 — 7 noviembre 2026 (importación de CHDMAN desde ZIP)

- Nuevo botón **Importar ZIP...** en la suite de discos. Permite elegir
  un paquete CHDMAN de Recalbox, OnionUI u otra fuente confiable sin
  descomprimirlo ni colocar manualmente el ejecutable.
- La GUI busca `chdman.exe` dentro del ZIP (incluidas subcarpetas),
  extrae únicamente ese ejecutable a la carpeta local
  `%LOCALAPPDATA%\\PSXPackagerLNR\\Tools` y recuerda su presencia.
- Al abrir la suite, detecta automáticamente un CHDMAN ubicado junto a
  la aplicación o el importado previamente. Se mantiene
  **Localizar...** para seleccionar un ejecutable ya instalado.
- La importación exige confirmación, verifica una firma básica de
  ejecutable PE y registra su huella SHA-256.
  **Esta comprobación no certifica que el binario sea seguro u oficial**.
- El fork **no redistribuye CHDMAN**: el usuario obtiene su propia copia
  de terceros. Se recomienda preferentemente la distribución oficial
  de MAME; Recalbox/OnionUI se documentan como alternativas ZIP.
- Se mantienen todas las funciones y créditos del software original;
  la conversión CHD→PBP sigue sin necesitar CHDMAN.

**Pendiente**: validar en Windows importando un ZIP real y comprobar
las tres rutas de conversión inversa. No publicar versión estable.

## v0.1.0-beta.6 — 7 noviembre 2026 (suite de discos: versión experimental)

- Nuevo módulo gráfico **Suite de discos** desde el menú principal y
  desde la ventana CHD→PBP, con conversiones por lotes:
  **PBP→CHD (PS1)**, **CHD→BIN/CUE (CD de PS1/Saturn/PS2)**
  y **CHD→ISO (DVD de PS2)**.
- PBP→CHD utiliza la extracción BIN/CUE del PSXPackager original,
  luego `chdman createcd` y `chdman verify` por disco, sin
  pasos manuales; temporales limpiados automáticamente.
- El modo CD usa `chdman extractcd` y recupera `CUE+BIN`;
  el modo DVD usa `chdman extractdvd` y genera `ISO`.
- Los originales se conservan siempre en la suite, incluso si falla
  una conversión; no se sobrescriben salidas existentes.
  Los resultados se guardan en `CHD`, `BIN_CUE` o `ISO`.
- **Dependencia separada**: estas conversiones nuevas requieren
  `chdman.exe` oficial de MAME, todavía NO incorporado al ZIP.
  Se puede localizar mediante la GUI o colocarlo junto al ejecutable.
  El modo principal CHD→PBP permanece independiente de CHDMAN.
- Nuevos manuales, explicación de los formatos CD/DVD y protección
  de pistas de audio.

**Pendiente de pruebas manuales**: extracción multidisco PBP→CHD, audio
de CD de Saturn/PS1 y extracción ISO de DVD PS2. No publicar estable.

## v0.1.0-beta.5 — 7 noviembre 2026 (compresión y borrado avanzado)

- Se añade **nivel 3** en el selector de compresión PBP, propuesto
  como equilibrio inicial LNR para consolas económicas. Se mantienen
  niveles 1/5/9. El motor original sigue teniendo el nivel 5 por defecto.
- La GUI guarda el **nivel numérico**, adaptando sin pérdida las
  preferencias de quienes habían seleccionado 1, 5 o 9 en beta anterior.
- Nueva casilla **Eliminar .CHD convertidos (irreversible)**, siempre
  desactivada en cada sesión y en cada lote, con confirmación doble.
- Borrado solo después de conversión nueva satisfactoria: valida cabecera
  y tamaño del PBP provisional y del definitivo; preserva CHD saltados,
  fallidos o cancelados y protege archivos modificados tras el análisis.
- El PBP provisional se genera en la misma unidad del resultado final;
  se mueve sin una segunda copia completa, aunque **sí requiere espacio
  para crear un PBP antes de eliminar el CHD**.
- Si falla la copia de un SBI auxiliar, se conservan los CHD; M3U/SBI
  nunca se eliminan. Registro indica cada archivo borrado o conservado.
- Nueva ventana **Ayuda y opciones**, integrada en la aplicación, y
  [manual en castellano](LNR/GUI-AYUDA.md) con objetivos del fork,
  funcionamiento, riesgos, pistas de audio y gestión de espacio.
- Toolkit BAT/PowerShell incorpora nivel 3; el Toolkit nunca borra CHD.

**Limitación de seguridad**: una validación de archivo PBP no garantiza
que funcione en una consola, y borrar archivos es irreversible. Requiere
prueba práctica explícita antes de recomendarlo públicamente.

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
