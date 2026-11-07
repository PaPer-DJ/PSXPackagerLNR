# PSXPackagerLNR — Manual de uso de la interfaz gráfica

> Edición LNR comunitaria del software **PSXPackager**. El motor original y la
> licencia MIT pertenecen a sus autores: copyright 2020 David Khristepher
> Santos; repositorio mantenido por RupertAvery y colaboradores. LaNaveRETRO
> aporta la experiencia de uso, automatización y funciones añadidas, **no
> reclama la creación del programa original**.

## 1. Objetivo de esta edición

Hacer que una colección de juegos de PlayStation 1 almacenados en archivos
`.CHD` pueda convertirse por lotes a `.PBP` **directamente**, sin tener
que abrir una consola de comandos, extraer previamente imágenes `.BIN/.CUE`,
instalar CHDMAN ni utilizar múltiples herramientas a mano. La conversión
CHD→PBP utiliza el lector CHD ya presente en el código de PSXPackager y no
necesita generar archivos BIN/CUE intermedios.

La interfaz permite seleccionar una carpeta, comprobar los juegos, convertir,
consultar estadísticas, cancelar y abrir los resultados. Los juegos y
firmware/BIOS **no** están incluidos.

## 2. Cómo abrir PSXPackagerLNR (Windows)

Al descargar y descomprimir el paquete **GUI-win-x64**, encontrarás
dos ejecutables gráficos en la misma carpeta:

- **`PSXPackagerLNR.exe` (recomendado)**: es un **pequeño lanzador
  nativo de Windows** que abre directamente la ventana
  **CHD → PBP por lotes**, con todas las herramientas LNR y el acceso
  a **Suite CD / CHD / ISO**. No necesita duplicar el ejecutable
  completo de la GUI ni pasar por la pantalla clásica.
- **`PSXPackagerGUI.exe` (opcional)**: conserva la interfaz original de
  PSXPackager, con su editor, opciones y funciones propias; también
  permite abrir las herramientas añadidas por LNR.

El lanzador `PSXPackagerLNR.exe` **no contiene otra copia de la
interfaz ni del motor**: llama a `PSXPackagerGUI.exe` con un
parámetro de inicio interno. También se incluye `psxpackager.exe`
(CLI) porque la GUI lo necesita para convertir. **Extrae todo
el ZIP y no muevas los ejecutables fuera de la carpeta**. Para
un icono en el escritorio, crea un acceso directo de Windows al
lanzador. La conversión CHD→PBP no precisa CHDMAN; algunas
conversiones de la Suite sí lo requieren.

## 2.1. Seleccionar carpeta y analizar

- **Ver carpetas...** muestra subcarpetas y una vista previa de CHD con tamaños.
  Pulsa **Usar esta carpeta** para confirmarla.
- La lista de juegos se actualiza al seleccionar una carpeta, restaurar la
  última utilizada, introducir una ruta y pulsar Intro, o cambiar el modo
  multidisco.
- **Analizar** fuerza una actualización manual. Solo se inspeccionan CHD
  directamente dentro de la carpeta, no las subcarpetas.
- **Nombre PBP, Discos, Origen, CHD, PBP, Variación, Tiempo y Estado** muestran
  nombre de destino, número de discos, método de agrupación, tamaño de entrada,
  tamaño de salida, diferencia de espacio y duración por juego.
- **Saltado / Ya existe** significa que ya hay un PBP con ese nombre: no se
  sobreescribe ni se elimina su CHD de origen.

## 3. Multidisco

- **Separados (1:1)** genera un PBP por CHD, manteniendo su nombre original.
- **Unidos (M3U)** intenta generar un solo PBP por juego multidisco,
  priorizando las listas M3U y detectando algunas series de discos numerados.
  Una lista M3U incompleta o incorrecta no debe convertirse a ciegas.
- **El cambio de disco depende del emulador**: no todos los dispositivos
  admiten los PBP multidisco. Prueba antes de convertir colecciones grandes.
- Las listas `.m3u` y archivos `.sbi` permanecen en la carpeta original.

## 4. Compresión PBP y justificación técnica del nivel 3

PSXPackagerLNR recomienda **3 — Equilibrada (LNR)** como selección inicial,
especialmente para preparar colecciones destinadas a consolas modestas.
Es un **compromiso práctico entre tamaño y velocidad de creación del PBP**:
no se ha demostrado que sea siempre el nivel más rápido al reproducir.

- **1 — Ligera (rápida)**: minimiza el esfuerzo al comprimir, pero a
  menudo produce archivos mayores; útil si prima el tiempo de conversión.
- **3 — Equilibrada (LNR, predeterminada)**: pertenece al grupo de
  búsqueda rápida 1–4 de SharpZipLib 1.4.2; preferido en esta edición
  por su equilibrio práctico de tiempo de creación y tamaño. El 4
  también usa búsqueda rápida, pero no garantiza comprimir mejor que 3.
- **5 — Estándar original**: nivel predeterminado del motor PSXPackager,
  emplea búsqueda más exhaustiva durante la compresión.
- **9 — Máxima**: búsqueda de coincidencias más costosa al comprimir;
  no garantiza producir siempre el archivo más pequeño para cada juego.

### Lo que está comprobado en el código

El fork usa **SharpZipLib 1.4.2** y llama a
`new Deflater(level, true)` para comprimir los sectores.
La tabla de funciones `COMPR_FUNC` de esa versión asigna:

| Nivel | Función del compresor | Consecuencia directa |
|---|---|---|
| 1, 2, 3 y 4 | `DEFLATE_FAST` | Búsqueda rápida de coincidencias al crear el archivo |
| 5, 6, 7, 8 y 9 | `DEFLATE_SLOW` | Búsqueda más exhaustiva al crear el archivo |

**Ojo con la diferencia entre implementaciones:** en **zlib clásico**
el cambio a `deflate_slow` sucede en el **nivel 4**, mientras que en
**SharpZipLib 1.4.2** (la biblioteca usada aquí) sucede en el **5**.
Por tanto, el resumen que atribuye ese salto al 4 no describe
exactamente el motor de esta aplicación.

**La búsqueda de coincidencias, la evaluación perezosa (*lazy match*),
las cadenas de hashes y el esfuerzo exhaustivo pertenecen al
COMPRESOR que ejecuta PSXPackager en el PC.** Cuando la consola lee
un PBP, su emulador solo interpreta el resultado de DEFLATE: no vuelve
a probar esas cadenas de coincidencias. En la
[especificación RFC 1950](https://www.rfc-editor.org/rfc/rfc1950),
los indicadores de intensidad de compresión tampoco son necesarios
para descomprimir. No se deduce de ahí ningún aumento automático de CPU,
caída de FPS o microtirones entre niveles 3, 5 y 9.

El tiempo de lectura real puede cambiar según **distribución de los
bloques comprimidos, acceso a la microSD, caché, emulador y hardware**,
pero hay que medirlo en la consola. El nivel 3 es una elección
sensata de partida, no el mejor nivel universal demostrado. Tampoco
es correcto prometer que 5–9 solo ahorran 1–2 %: depende del juego;
algunas imágenes PBP incluso pueden ocupar más que sus CHD de origen.

**Fuentes técnicas verificadas:**

- [Código de compresión de este fork](../Popstation/Compression.cs).
- [Versión SharpZipLib utilizada](../Popstation/Popstation.csproj).
- [Tabla `COMPR_FUNC` de SharpZipLib 1.4.2](https://github.com/icsharpcode/SharpZipLib/blob/v1.4.2/src/ICSharpCode.SharpZipLib/Zip/Compression/DeflaterConstants.cs).
- [Tabla de estrategias del zlib clásico](https://github.com/madler/zlib/blob/develop/deflate.c).
- [RFC 1950 — indicadores de nivel de compresión](https://www.rfc-editor.org/rfc/rfc1950).

### Pruebas en PSP original realizadas por LaNaveRETRO

En pruebas prácticas con una **PSP original**, comparando juegos de PS1
convertidos a PBP con distintos niveles, **los niveles 1 a 3 han mostrado
menores pausas de carga que los niveles 4 o 5 en determinados momentos**.
Se trata de una observación directa sobre el dispositivo real,
no de una deducción a partir de `DEFLATE_FAST` o `DEFLATE_SLOW`.

En los juegos evaluados, al pasar del **nivel 4 al 5** se obtuvieron
**solo algunos megabytes adicionales de reducción**; entre los niveles
**5 y 9** el tamaño final cambió muy poco. Esta diferencia puede variar
según el contenido del juego; no representa una media estadística
de todo el catálogo de PS1 ni garantiza un tamaño determinado.

**Elección LNR: nivel 3 por defecto**, especialmente como punto de
partida para PBP destinados a PSP original. Se recomienda probar
también 1 o 2 si algún juego sigue mostrando pausas. El nivel 3
**no garantiza mejores FPS** ni elimina necesariamente problemas
propios del emulador, la tarjeta de memoria o la consola.

Desde esta edición, los valores predeterminados son **3 en la GUI
clásica, 3 en la GUI por lotes y 3 en la CLI del fork LNR**.
Se siguen respetando los ajustes ya guardados por cada usuario;
seleccionar manualmente 1, 5 o 9 sigue siendo posible.
**El proyecto original de RupertAvery permanece intacto y conserva
su valor por defecto de 5**: el cambio a 3 es exclusivo del fork LNR.

## 5. Convertir y resultados

1. Revisa los juegos y el modo elegido.
2. Deja **Eliminar .CHD convertidos** desactivado para la opción segura.
3. Pulsa **Convertir** y acepta el resumen mostrado.
4. Los PBP aparecen en la subcarpeta `PBP` de la carpeta seleccionada.
5. Un **✔ verde** confirma que se creó el PBP, un **✖ rojo** indica error,
   y un **aviso amarillo** indica un PBP existente que se ha respetado.

El porcentaje de la fila es el de la **fase** de conversión, no el de todo
el juego. La barra inferior indica cuántos juegos del lote se han
procesado y ofrece un tiempo restante **aproximado**, calibrado tras
una conversión válida. **Copiar registro** facilita informar errores;
**Limpiar registro** (situado antes de **Copiar registro**) vacía el panel **Actividad y resultados** y el
historial en memoria desde la última limpieza, de modo que **Copiar registro**
solo copia los mensajes nuevos. **No borra informes TXT ya guardados**,
los CHD ni los PBP, y no detiene ninguna conversión. Si hay un lote en
marcha, sus siguientes mensajes continuarán apareciendo en el panel.
**Abrir carpeta PBP** abre los resultados. Puedes ampliar el registro
arrastrando el separador y desmarcar **Seguir actividad** para consultarlo.

### Columnas, progreso de fase y duración total (beta.10)

El reparto de espacio inicial también se ha mejorado: **Actividad y
resultados** empieza a **200 px** en lugar de 300, por lo que
**Juegos CHD detectados** ocupa una superficie mayor, similar al diseño
de referencia. El separador sigue siendo arrastrable. La primera
apertura de beta.10 migra el antiguo valor guardado de 300 px; si habías
elegido otro tamaño, se conserva.

En la tabla, **Nombre PBP** permanece alineado a la izquierda; las columnas
**Discos, Origen, CHD, PBP, Variación, Tiempo y Estado/Progreso** se centran.
Los anchos de las columnas de datos se calculan con el texto visible y
la columna de estado aprovecha el espacio restante. En ventanas estrechas
se mantiene un desplazamiento horizontal en vez de comprimir la barra.
Durante cada conversión, la barra verde se extiende hasta justo antes
del porcentaje. El porcentaje es **de la fase**, no del juego completo.

Al finalizar, la barra inferior indica **«Lote Finalizado · Tiempo total:
mm:ss»** o, si ha durado una hora o más, **hh:mm:ss**. También se informa
si el lote ha sido cancelado o ha terminado con errores. El tiempo
cuenta desde el comienzo de la conversión del lote hasta su finalización,
incluidos los archivos saltados.

### Apagar equipo al finalizar (beta.10)

- La casilla **Apagar equipo al finalizar el lote** aparece en la parte
  inferior derecha, desmarcada por defecto. **Nunca se recuerda marcada.**
- Requiere confirmación antes de empezar; se considera una acción
  independiente de la opción de borrado de CHD.
- Solo se programa si **al menos un juego se ha convertido** y todos los
  trabajos terminan sin error ni cancelación ni advertencias de borrado.
  Si todo se salta porque ya existe, **no apaga el equipo**.
- Después del lote empieza una **cuenta atrás local de 60 segundos** en
  la GUI. Se puede anular con **Cancelar apagado programado**. Cerrar la
  aplicación durante la cuenta atrás también cancela el apagado.
- Al terminar la cuenta atrás, Windows recibe una solicitud de apagado
  **normal y sin forzar aplicaciones** (`shutdown.exe /s /t 0`).
  Windows puede aplazarlo o impedirlo si hay documentos sin guardar.
- No se usa `shutdown /t 60`, porque en Windows los retardos positivos
  llevan implícito un cierre forzado de programas, con posible pérdida
  de datos. El apagado depende de que la GUI siga abierta durante su
  cuenta atrás; no se agenda en segundo plano.

**Importante:** comprueba que los trabajos ajenos al conversor están
guardados y que no hay otras tareas importantes en ejecución antes de
activar esta opción. Es una función avanzada y no está habilitada por
defecto.

## 6. Eliminar CHD convertidos — función avanzada

### ¿Para qué existe esta opción?

Esta función está pensada especialmente para quien necesita **convertir
colecciones muy grandes de juegos PS1 de CHD a PBP en el mismo disco
duro, sin tener espacio libre para conservar simultáneamente los dos
catálogos completos**.

En una conversión normal, los CHD originales siguen ocupando espacio
y se van acumulando los PBP creados. Al finalizar, quedan **ambas
colecciones** en el disco: si los PBP tienen un tamaño parecido al de
sus CHD de origen, la ocupación puede acercarse al doble; incluso
puede superarlo cuando los PBP son mayores que los CHD.

Activando **Eliminar .CHD convertidos**, tras **cada conversión nueva
terminada correctamente y con las comprobaciones básicas superadas**,
la aplicación elimina los CHD que se hayan convertido, **liberando su
espacio antes de comenzar a acumular el resto del lote**. De este
modo no es necesario reservar espacio para mantener *toda* la colección
en CHD **y** *toda* la colección en PBP al mismo tiempo.

**Ejemplo orientativo:** si una colección CHD ocupa 100 GB y los PBP
generados terminan ocupando 110 GB, **sin borrar** se necesitarían
aproximadamente **210 GB** para conservar ambas colecciones. Con
la eliminación activada, los CHD de los trabajos correctos se van
sustituyendo progresivamente por sus PBP; no se acumulan las dos
colecciones completas. El espacio máximo necesario dependerá de los
tamaños reales y del orden de procesamiento.

**Si quieres conservar tus CHD originales, NO actives esta casilla.**
Es una opción voluntaria para ahorrar espacio, **desactivada en cada
inicio y en cada lote; nunca se recuerda activada**. Su uso requiere
otra confirmación porque el borrado es irreversible.

### ¿Y si después necesito volver a tener mis juegos en CHD?

La propia **Suite CD / CHD / ISO** de PSXPackagerLNR incluye el modo
**PBP → CHD (PS1)**, que permite procesar lotes y reconstruir
un CHD por disco, incluso a partir de PBP multidisco. Este modo
necesita CHDMAN y utiliza BIN/CUE temporales.

Esto ofrece una **vía práctica de vuelta a CHD** si en el futuro
cambias de consola o de emulador. **Sin embargo, no equivale a
restaurar exactamente el CHD original ni garantiza una conversión
reversible sin pérdidas en todos los casos**: un PBP puede no conservar
ciertos datos de subcanal, información de protección, metadatos o
detalles de la imagen original. Lo que no haya quedado almacenado
en el PBP no puede recuperarse recreando un CHD. Los archivos SBI
que se hayan conservado deben guardarse también si son necesarios.

**Para una copia de preservación fiel del disco original, guarda
los CHD aparte**. La conversión inversa sirve para obtener un CHD
utilizable a partir del contenido disponible en el PBP, no para
garantizar una imagen idéntica bit a bit.

### Cómo funciona el borrado y qué comprueba

Al activarla, la aplicación solicita una **confirmación adicional**
de borrado definitivo. Tras una conversión nueva, el programa:

1. Comprueba que se generó un único PBP, que tiene una longitud mínima
   y que empieza por la firma reconocible `00 50 42 50`.
2. Mueve el PBP provisional a su ubicación final en la misma unidad
   y vuelve a comprobar su tamaño y firma.
3. Conserva los archivos auxiliares M3U y SBI. Si falla la copia
   de algún SBI correspondiente al juego, no elimina sus CHD.
4. Comprueba que el CHD de origen no ha cambiado desde el análisis
   y que no es un enlace simbólico; **solo entonces intenta borrarlo**.

**Nunca elimina** los CHD de trabajos saltados porque el PBP ya
existía, ni de conversiones fallidas o canceladas. Los archivos
borrados **no pasan por la Papelera de Windows**.

**Límite de seguridad:** verificar el tamaño y la cabecera del PBP
**no garantiza** que todos los sectores se lean correctamente ni
que el juego funcione en tu PSP u otro emulador. Si pretendes borrar
los originales, prueba antes varios juegos representativos, incluyendo
multidisco, pistas CD-Audio y juegos que utilicen SBI.

### ¿Cuánto espacio libre necesito al convertir?

La conversión principal CHD → PBP **no crea archivos BIN/CUE
intermedios**, pero **sí necesita escribir por completo el PBP
del juego actual antes de eliminar sus CHD de origen**. Por ello,
el disco necesita espacio libre para ese PBP, y especialmente
para el PBP de mayor tamaño que tenga que generar (por ejemplo,
un juego multidisco unido).

El PBP provisional se crea dentro de una carpeta temporal de
`PBP`, en el **mismo volumen de destino**, y después se mueve a
su nombre definitivo sin copiar íntegramente el archivo de nuevo.
Tras cada borrado correcto se libera espacio que podrá utilizarse
en las siguientes conversiones.

**Esta función reduce la acumulación de espacio, pero no permite
convertir sin espacio libre inicial**. Tampoco garantiza que la
ocupación final sea menor que la inicial: dependerá de si tus PBP
son más pequeños o grandes que sus CHD correspondientes.

## 7. Conversión inversa y formatos de disco: alcance de la suite

La suite se amplía en beta.8 y aparece mediante **Suite CD / CHD / ISO**
en el conversor y como entrada independiente en el menú principal.
Dispone de **cinco modos**:

| Dirección | Salida | Limitaciones |
|---|---|---|
| PBP → CHD | `CHD/Nombre.chd`, uno por disco PS1 | PSXPackager extrae temporalmente BIN/CUE y CHDMAN genera el CHD; no se recuperan subcanales ya perdidos |
| PBP → BIN/CUE | `BIN_CUE/Juego/` con CUE/BIN de cada disco | PS1, sin CHDMAN; recupera pistas disponibles, pero no datos que ya se perdieron al crear el PBP |
| PBP → ISO de datos | `ISO/Juego.iso` | Solo 1 disco y 1 pista MODE1/2048, MODE1/2352 o MODE2 Form1/2352; rechaza CD-Audio, XA Form2 y multidisco |
| CHD de CD → BIN/CUE | `BIN_CUE/Juego/Juego.cue` y `.bin` | PS1, Saturn y PS2 en CD; conserva las pistas de audio disponibles en el CHD |
| CHD de DVD → ISO | `ISO/Juego.iso` | PS2 en DVD, no CD; usa `chdman extractdvd` |

**PBP → ISO** NO es una reconstrucción fiel de un CD para grabar.
Únicamente extrae los 2048 bytes útiles por sector cuando todo el disco
es una sola pista de datos compatible. No conserva pistas CD-Audio,
sectores Mode2 Form2/XA, subcanales ni protección; esos casos generan
un error explícito para evitar ISOs engañosamente incompletos. Para
juegos de PS1 que quieras conservar o grabar, utiliza **PBP → BIN/CUE**
y comprueba las pistas. No hace falta instalar CHDMAN para ninguna
de estas dos conversiones de PBP (BIN/CUE, ISO limitado).

**Funcionamiento de la suite (beta experimental):**

1. Selecciona una de las tres conversiones en la lista desplegable.
2. Indica la carpeta de origen. Pulsa **Analizar** y revisa los archivos.
3. Para **PBP→CHD y CHD→BIN/CUE o ISO** consigue una copia legítima de **CHDMAN**.
   **PBP→BIN/CUE y PBP→ISO de datos no la necesitan.** La fuente preferida es
   [MAME oficial](https://www.mamedev.org/release.html).
   También hay ZIP prácticos en
   [Recalbox](https://wiki.recalbox.com/es/tutorials/utilities/rom-conversion/chdman)
   y [OnionUI](https://onionui.github.io/docs/advanced/chdman).
   La versión incluida en el enlace de OnionUI es 0.251 y puede ser
   anterior a la versión oficial actual.
4. Pulsa **Importar ZIP...**: se abrirá una ventana con enlaces
   clicables a **Recalbox**, **OnionUI** y **MAME**. Los enlaces
   abren las fuentes en el navegador sin cerrar el aviso; así puedes
   descargar el paquete **antes** de responder. Después pulsa
   **Sí, importar ZIP** para seleccionar el ZIP ya descargado, o
   **No** para cancelar. Abrir un enlace no descarga ni ejecuta nada
   automáticamente.
   LNR extraerá únicamente `chdman.exe` a
   `%LOCALAPPDATA%\\PSXPackagerLNR\\Tools` y lo reconocerá
   automáticamente en las siguientes sesiones; no tienes que copiar
   archivos manualmente. En la importación se comprueba una cabecera
   de ejecutable Windows y se registra SHA-256, pero esto NO garantiza
   autenticidad ni ausencia de malware: usa únicamente paquetes fiables.
   Alternativamente puedes poner `chdman.exe` junto a la GUI o
   pulsar **Localizar...** para usar uno ya instalado. El ZIP de LNR
   no redistribuye el ejecutable de MAME.
5. Pulsa **Convertir lote** y confirma. El programa conserva **todos**
   los originales y no sobrescribe los destinos existentes.
6. Se limpian automáticamente los temporales de cada juego; no se
   borran archivos de salida completados antes de un error/cancelación.


**PBP → CHD no es una conversión directa sin intermediarios**: para
reconstruir un PBP se necesita extraer BIN/CUE en una carpeta temporal,
crear el CHD con CHDMAN y comprobarlo con `chdman verify`. El usuario
no tiene que gestionar manualmente esos archivos, pero necesita suficiente
espacio libre para la extracción temporal. En un PBP multidisco, se
generan varios CHD independientes.

**CHD → BIN/CUE no graba físicamente ningún disco.** Produce las
imágenes que puedes abrir en un programa de grabación compatible con
pistas múltiples. Para PS2 DVD, genera ISO y utiliza un DVD adecuado.
La grabación física, posibles protecciones, subcanales y compatibilidad
con el hardware original dependen de las imágenes y del dispositivo.

Las operaciones con CHDMAN están **separadas** de la función directa
CHD→PBP, que sigue funcionando **sin CHDMAN**. Fuentes oficiales:
<https://www.mamedev.org/release.html> y
<https://docs.mamedev.org/tools/chdman.html>.

Un `.PBP` de PS1 no es un formato para Saturn ni PS2. Los `.CHD` de
DVD deben restaurarse como ISO y grabarse, si procede, en soporte DVD,
no en CD. No todos los juegos con protecciones/subcanales pueden
reconstruirse a partir de una conversión previa a PBP.

## 8. Créditos y responsabilidad

PSXPackagerLNR es una adaptación comunitaria, no una edición oficial
avalada por el autor original. Se mantiene su licencia MIT y se
distribuye el código fuente con los créditos. LaNaveRETRO no garantiza
compatibilidad con todas las consolas ni integridad de todos los
resultados en cualquier entorno. Usa las herramientas solo con imágenes
que tengas derecho a copiar o respaldar.

- Proyecto original: <https://github.com/RupertAvery/PSXPackager>
- Fork LNR: <https://github.com/PaPer-DJ/PSXPackagerLNR>
- Documentación técnica CHDMAN: <https://docs.mamedev.org/tools/chdman.html>

### Anexo técnico del fork: compresión PBP nivel 3

Para quienes quieran conocer el motivo detallado de esta decisión,
hemos añadido al final de la parte LNR del
[README del fork](../README.md#anexo-tecnico-lnr-nivel-3)
un análisis con las **pruebas realizadas por LaNaveRETRO en PSP
original**, los parámetros exactos de SharpZipLib 1.4.2 para los
niveles 1 a 9 y la explicación de LZ77, Huffman, greedy/lazy match
y cadenas hash.

Es importante distinguir **las pausas de carga observadas en PSP**
del **trabajo de compresión que realiza el PC**. Los parámetros
de búsqueda de coincidencias no se vuelven a calcular durante
la descompresión, y las pruebas no constituyen una medición aislada
del consumo de CPU de la PSP. El nivel 3 se mantiene como
**recomendación práctica de LNR**, no como promesa universal de
rendimiento superior para todos los juegos.
