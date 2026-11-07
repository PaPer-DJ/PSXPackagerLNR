# PSXPackagerLNR - LaNaveRETRO Edition (fork)

Esta es una **adaptacion y automatizacion comunitaria** del proyecto original
[PSXPackager de RupertAvery](https://github.com/RupertAvery/PSXPackager).
**No es un programa creado originalmente por LaNaveRETRO**. Se conservan
la licencia MIT y los creditos del software original.

## Versionado y descargas identificables

La version de desarrollo actual es **v0.1.0-beta.14** (nivel 3 predeterminado en todas las interfaces LNR, con pruebas reales de carga en PSP).
La version se define en [LNR/VERSION](LNR/VERSION) y sus novedades
se documentan en [CHANGELOG-LNR.md](CHANGELOG-LNR.md).

**Cada compilacion de GitHub Actions lleva ademas un numero irrepetible**.
Por ejemplo: `PSXPackagerLNR-v0.1.0-beta.14-bNN-GUI-win-x64.zip`.
Los otros paquetes tienen el mismo prefijo y terminan en `CLI-win-x64.zip`
y `Toolkit-win-x64.zip`.

El titulo de la interfaz y el registro muestran exactamente la version y
compilacion que se esta ejecutando. No hay que renombrar manualmente ZIPs.

- [Descargas de pruebas (Actions)](https://github.com/PaPer-DJ/PSXPackagerLNR/actions).
- Las builds de Actions son **temporales** y pueden caducar a los 30 dias.
  Conserva los ZIP que quieras probar o comparar.
- [Releases](https://github.com/PaPer-DJ/PSXPackagerLNR/releases) se usara
  para versiones publicadas y archivadas permanentemente tras validacion.
- De momento la rama `master` se mantiene independiente del trabajo en
  `lnr/windows-chd-build`. No hay release estable de LNR.

### Por qué LNR recomienda compresión PBP nivel 3

El motor de [PSXPackager utiliza SharpZipLib 1.4.2](Popstation/Popstation.csproj)
para la compresión DEFLATE del contenido del PBP. La
[tabla real de SharpZipLib 1.4.2](https://github.com/icsharpcode/SharpZipLib/blob/v1.4.2/src/ICSharpCode.SharpZipLib/Zip/Compression/DeflaterConstants.cs)
emplea `DEFLATE_FAST` en los **niveles 1–4** y `DEFLATE_SLOW`
en los **niveles 5–9**.

En esta versión LNR se preselecciona **3 — Equilibrada (LNR)**
en **la GUI clásica, la GUI por lotes y la CLI**.
Hay dos motivos diferentes para esta decisión:

1. **Pruebas prácticas en PSP original por LaNaveRETRO:** en ciertos
   momentos de carga de algunos juegos PS1, los archivos PBP creados
   con niveles **1–3 cargaron antes** que los creados con 4 o 5.
   En los juegos probados, pasar de nivel 4 a 5 ahorró solo unos
   pocos MB, y pasar de 5 a 9 aportó muy poco espacio adicional.
   Estos son resultados de pruebas propias, **no una media validada
   de todo el catálogo ni una garantía para cualquier PSP o emulador**.
2. **Velocidad de generación en PC:** el nivel 3 utiliza la búsqueda
   rápida de SharpZipLib y ofrece un compromiso de tamaño/tiempo
   de creación. Los parámetros greedy/lazy y las cadenas hash
   afectan al **compresor que crea el PBP**: el emulador no repite
   esas búsquedas al descomprimirlo.

La diferencia de tiempo de carga real debe medirse en el dispositivo;
puede depender también del emulador, su caché y la tarjeta de memoria.
**No se promete un aumento automático de FPS.**

**Dato importante:** zlib clásico pasa de compresión rápida a
búsqueda más intensiva en el **nivel 4**, pero SharpZipLib 1.4.2
lo hace en el **nivel 5**. El propio nivel 4 de SharpZipLib
no garantiza ser mejor que 3, porque algunos parámetros de búsqueda
son menos exhaustivos que en nivel 3. Así, **3 es la recomendación
técnica LNR**, no un óptimo universal de velocidad de descompresión
demostrado. Se conservan las opciones 1, 5 y 9, así como la
preferencia previamente guardada por cada usuario.

Hay también un [**anexo técnico completo al final de la información LNR**](#anexo-tecnico-lnr-nivel-3),
con la tabla exacta de parámetros 1–9, LZ77/Huffman, greedy/lazy match,
las pruebas en PSP y las limitaciones de la conclusión. El resumen
de uso se encuentra en
[la guía de compresión PBP](LNR/GUI-AYUDA.md#4-compresión-pbp-y-justificación-técnica-del-nivel-3).
**En el fork LNR se utiliza 3 por defecto en todas las interfaces**.
El PSXPackager original de RupertAvery no se modifica y sigue
teniendo nivel 5 como opción predeterminada en su propio código.
Las preferencias previamente guardadas por el usuario se respetan.

### Importar CHDMAN: tres fuentes accesibles desde el programa

Al pulsar **Importar ZIP...** aparece una ventana con enlaces
clicables a [Recalbox](https://wiki.recalbox.com/es/tutorials/utilities/rom-conversion/chdman),
[OnionUI](https://onionui.github.io/docs/advanced/chdman) y
[MAME oficial](https://www.mamedev.org/release.html).
Abren el navegador **sin cerrar la confirmación**, de modo que puedes
descargar primero el paquete y después elegir **Sí, importar ZIP**
para seleccionar el archivo local, o **No** para cancelar. Abrir
una de esas páginas no inicia ninguna importación ni ejecuta archivos.

### Limpiar y copiar registro

En **Actividad y resultados**, el nuevo botón **Limpiar registro**
vacía solo el panel y los mensajes de esta sesión acumulados desde la
última limpieza. Se sitúa antes de **Copiar registro**, seguido de **Abrir carpeta PBP**.
No elimina los informes TXT ya guardados, no afecta a archivos de juegos
y no cancela las conversiones: los nuevos mensajes siguen apareciendo.
Tras limpiar, **Copiar registro** recoge únicamente los mensajes nuevos.

### Interfaz del lote: nuevo reparto y apagado opcional

La lista de juegos CHD tiene más espacio al iniciar el programa:
el panel **Actividad y resultados** empieza a **200 px de altura**,
frente a los 300 px de betas anteriores. La posición del separador
sigue siendo redimensionable y personalizable; se adapta una sola vez
el antiguo tamaño predeterminado.

La tabla centra sus columnas numéricas, las autoajusta y deja
la mayor parte del espacio restante a **Estado/Progreso**. Al acabar
se indica **Lote Finalizado · Tiempo total**.

La casilla **Apagar equipo al finalizar el lote** está siempre desactivada
por defecto y exige confirmación. Solo se activa si se ha completado
al menos una conversión sin errores, cancelación ni problemas de
eliminación. Se inicia una cuenta atrás local de **60 segundos**
con botón Cancelar. El cierre de la GUI durante ese tiempo también
cancela la petición. Después se solicita un apagado normal de Windows
(`shutdown.exe /s /t 0`), sin forzar el cierre de programas. **Guarda
antes tus trabajos abiertos**: Windows puede impedir el apagado si hay
archivos sin guardar.

### Identidad visual y barra de progreso del lote

El logotipo original aportado **LNR MOD** se usa como sello pequeño y
discreto de la edición LNR, no como sustitución del programa original.
Pulsando sobre el sello en la ventana de conversión, o desde el menú
**Acerca de LNR...**, se abren los créditos originales, la licencia MIT
y enlaces al proyecto original y al fork.

La **barra inferior** expresa el porcentaje de juegos terminados
(correctos, saltados o errores) del lote. Es un porcentaje **exacto por
número de juegos**, no la media de fases ni el avance en bytes de un
PBP en curso.

Tras completar al menos una conversión, se muestra la **estimación
aproximada del tiempo restante** basada en el tamaño de los CHD pendientes
y los segundos por MB observados en conversiones correctas del lote.
Al principio aparece «calculando»; el tiempo puede variar mucho según
el juego y el dispositivo. No se promete una hora de finalización exacta.

## Guía de opciones y seguridad

La finalidad de **PSXPackagerLNR** es facilitar convertir **colecciones
de PlayStation 1 en CHD directamente a PBP**, en lotes y con una GUI,
sin comandos, CHDMAN ni conversión intermedia a BIN/CUE.

La función principal no ha cambiado. Solo si habilitas expresamente
**Eliminar CHD convertidos** se intentan borrar los CHD al terminar
correctamente un PBP nuevo. Esa opción siempre está desactivada
al abrir la ventana o iniciar un lote, exige otra confirmación y
**el borrado es irreversible**. No es una verificación de compatibilidad
de emulador. Al menos debe haber espacio libre para generar el PBP
antes de borrar el CHD. Los PBP provisionales se escriben en el mismo
volumen de destino para evitar copias completas innecesarias.

[Manual completo en castellano y explicación de todas las opciones](LNR/GUI-AYUDA.md).

### Suite de discos (beta experimental)

Desde el menú **Suite de discos · PBP/CHD/CD/DVD** también puedes
realizar conversiones por lotes:

- **PBP → CHD (PS1)**: genera un CHD por disco, incluso desde PBP
  multidisco, tras extracción temporal automatizada a BIN/CUE.
- **PBP → BIN/CUE (PS1)**: extrae pistas de uno o varios discos a BIN/CUE
  desde PBP sin CHDMAN, conservando la estructura disponible.
- **PBP → ISO (PS1)**: solo una pista de datos MODE1 o MODE2 Form1 de
  2048 bytes por sector. Rechaza audio CD, XA Form2, multidisco y demás
  casos que perderían información. Para conservación y grabación de PS1,
  **usa BIN/CUE** en vez de ISO.
- **CHD → BIN/CUE (CD)**: imágenes para aplicaciones de grabación de CD,
  con pistas CD-Audio cuando se encuentren en la imagen original.
  Dirigido a PS1, Saturn y PS2 publicado en CD.
- **CHD → ISO (DVD)**: para juegos PS2 publicados en DVD; no se deben
  convertir indiscriminadamente discos DVD a un BIN/CUE de CD.

**Importante:** los modos **PBP→CHD, CHD→BIN/CUE y CHD→ISO**
requieren `chdman.exe` de MAME. **PBP→BIN/CUE y PBP→ISO limitado
no lo necesitan.** Esta beta **no redistribuye CHDMAN**.
Puedes obtener por tu cuenta un paquete ZIP de
[Recalbox](https://wiki.recalbox.com/es/tutorials/utilities/rom-conversion/chdman)
o [OnionUI](https://onionui.github.io/docs/advanced/chdman)
y pulsar **Importar ZIP...** desde la suite. La aplicación extrae solo
`chdman.exe` a `%LOCALAPPDATA%\\PSXPackagerLNR\\Tools` y lo
detecta automáticamente en siguientes sesiones. También puedes
colocarlo junto a la GUI o pulsar **Localizar...**. Consulta
[la fuente oficial de MAME](https://www.mamedev.org/release.html) y
[su documentación CHDMAN](https://docs.mamedev.org/tools/chdman.html).
La suite no sobrescribe ni elimina archivos de origen, y conserva
cualquier resultado correcto producido antes de un error.

**Pendiente de validar en equipos reales:** pista de audio en Saturn/PS1,
PBP multidisco a CHD y CHD de DVD PS2 a ISO. No asegura recuperación
de información de subcanales perdida en un PBP preexistente.

## Estado de desarrollo

Estamos validando la conversion directa **CHD a PBP sin CHDMAN**, incluida
la gestion de pistas de audio CD y archivos multidisco. La version de pruebas
para Windows x64 incluye la interfaz grafica original **con una ventana
adicional LNR: CHD a PBP por lotes**, la CLI compilada desde el codigo fuente
mas reciente y un **Toolkit portable** con un conversor por lotes
alternativo basado en BAT/PowerShell.

### Usar la nueva interfaz grafica LNR (version de prueba)

1. En [GitHub Actions](https://github.com/PaPer-DJ/PSXPackagerLNR/actions)
   descarga el artefacto Windows mas reciente de la rama
   `lnr/windows-chd-build`.
2. Extrae el ZIP numerado que termina en `-GUI-win-x64.zip` completo en una carpeta
   de Windows (no ejecutes el programa directamente dentro del ZIP).
3. Inicia `PSXPackagerGUI.exe` y pulsa el nuevo menu
   **CHD a PBP - LNR**.
4. Pulsa **Ver carpetas...**. El explorador LNR muestra **subcarpetas
   a la izquierda y los CHD con su tamaño a la derecha**, para comprobar
   su contenido antes de pulsar **Usar esta carpeta**. También puedes
   pegar una ruta y pulsar Intro.
5. La tabla se **carga automaticamente** tras seleccionar la carpeta
   (tambien al recuperar la ultima carpeta o cambiar de modo). El botón
   **Analizar** queda disponible para forzar una nueva lectura.
6. Elige discos **separados** o **unidos** y nivel **1, 3, 5 o 9**
   (3 es la opción inicial LNR; 5 sigue siendo el nivel estándar
   original). Revisa los PBP existentes y pulsa **Convertir**.
7. Deja **Eliminar .CHD convertidos** desactivado si deseas conservar
   los originales. Si lo activas, la aplicación exige confirmación
   adicional y solo intentará borrar CHD de conversiones nuevas y
   comprobadas, nunca de errores ni destinos preexistentes. El borrado
   directo es irreversible y la comprobación no garantiza compatibilidad.

La distribucion GUI incluye tambien `psxpackager.exe`, necesario para
la conversion directa. No requiere PowerShell ni CHDMAN; el Toolkit BAT
sigue disponible como alternativa. La ventana muestra **porcentaje de progreso real de la fase de conversión**
en cada juego, leido en directo del motor CLI (no es una estimacion). Las
fases internas pueden empezar de nuevo en 0%, porque cada una publica su
propio progreso; la barra inferior indica los juegos completados del lote.
Tambien incluye cancelacion, comprobacion basica de salida y registro de
resultados **ampliable hacia arriba o abajo arrastrando el separador**.

Los estados de cada fila llevan indicadores diferentes:
- **Completado**: marca verde.
- **ERROR**: aspa roja.
- **Saltado / Ya existe**: advertencia amarilla (nunca sobrescribe).
- **Convirtiendo**: porcentaje y barra de avance de la fase actual.

En el registro, cada mensaje informativo se muestra en una linea
independiente. El aviso de confirmacion separa carpeta de destino,
modo, compresion y avisos de seguridad.

**Novedades de beta.3**: la tabla contiene **CHD (origen), PBP
(destino), variacion con signo y tiempo de conversion** por juego. El
porcentaje de progreso solo aparece al lado de la barra, no duplicado
en el nombre de fase. Los PBP existentes muestran su tamaño al
analizar. En caso multidisco el tamaño CHD es la suma de discos.
La variacion indica **PBP - CHD**: positiva si crece y negativa si
disminuye; no es una estimacion de compresion. Los tamaños se muestran
en MiB como «MB» segun Windows (1 MB = 1 048 576 bytes). Pasando el
raton por la variacion se ve el porcentaje relativo.

**Novedades de beta.2**: botones **Copiar registro** y **Abrir carpeta PBP**,
casilla **Seguir actividad** (puedes desmarcarla para revisar el registro
sin saltos al final), ayudas y nombres de fases en castellano.
La aplicacion guarda localmente la carpeta, el modo multidisco, nivel
de compresion, tamaño de ventana y panel del registro, y la casilla
de seguimiento al cerrar. El archivo es
`%LOCALAPPDATA%\\PSXPackagerLNR\\gui-preferencias.json`;
puedes borrarlo si deseas volver a los valores iniciales. No contiene
ROMs ni modifica los ajustes del PSXPackager original.

**Validado por GitHub Actions:** compila la GUI/CLI y supera pruebas
del planificador del BAT y de ejecución de la CLI redirigida.
**Ya probado por un usuario:** conversión desde GUI de Actua Soccer 3
y Tomb Raider. **Pendiente para beta.2:** comprobación de las nuevas
opciones visuales, prueba multidisco y cambio real de disco.

- [Instrucciones del Toolkit](LNR/Batch/README.md)
- [Compilaciones Windows y artefactos ZIP](https://github.com/PaPer-DJ/PSXPackagerLNR/actions)
- [Solicitud de cambios en desarrollo](https://github.com/PaPer-DJ/PSXPackagerLNR/pull/1)

El BAT ofrece discos **separados** (nombres exactos) o **unidos** (M3U) y
niveles de compresion **1, 3, 5 y 9**; el nivel 3 es la recomendación
inicial LNR. El Toolkit BAT no elimina nunca archivos CHD.
Los juegos, BIOS y archivos protegidos por derechos de terceros **no se
distribuyen** con el proyecto.

> Nota: una conversion PBP correcta no garantiza que todos los emuladores
> admitan audio CD, SBI o cambios de disco de la misma manera. Comprueba
> cada plataforma. Los ejecutables siguen conservando los nombres originales
> durante esta fase de validacion.

## Anexo tecnico LNR nivel 3

### Por qué LaNaveRETRO ha elegido este nivel

**El nivel 3 es la opción predeterminada de PSXPackagerLNR**, especialmente
pensando en juegos de PS1 convertidos a `.PBP` para **PSP originales** y
otros dispositivos con recursos limitados. No se eligió solo porque
la compresión tarde menos en el PC: es el resultado de **pruebas
prácticas de LaNaveRETRO sobre una PSP real**.

En algunos juegos de esas pruebas, los PBP comprimidos con **niveles 1,
2 o 3** produjeron **menores esperas en determinados momentos de carga**
que sus versiones con niveles **4 o 5**. Al comparar tamaños, el salto
**4 → 5** apenas recuperó unos pocos megabytes y **5 → 9** ofreció
una ganancia adicional muy pequeña en los títulos examinados.

Eso fundamenta la preferencia práctica por el 3, pero conviene separar
tres conceptos distintos:

1. **Tiempo de compresión:** cuánto trabaja el PC para generar el PBP.
2. **Tamaño resultante:** espacio ocupado por el PBP y cantidad de datos
   comprimidos que habrá que leer de la Memory Stick/microSD.
3. **Tiempo de carga y coste de descompresión:** lo que realmente ocurre
   en la PSP al leer y reconstruir los datos durante el juego.

La prueba en PSP avala el **resultado observado de carga en esos juegos**,
pero no demuestra por sí sola cuánto porcentaje de CPU consumió
la descompresión ni que se deba exclusivamente al nivel seleccionado.
Para demostrar esa causalidad harían falta mediciones comparables de
tiempo de lectura, datos comprimidos, caché y actividad de CPU.

### Cómo funciona realmente DEFLATE dentro de PSXPackagerLNR

El motor del fork emplea **SharpZipLib 1.4.2**. Para comprimir utiliza
`new Deflater(level, true)`, es decir, **DEFLATE sin cabecera zlib**,
aplicado a los bloques que almacena el formato PBP.

**DEFLATE combina LZ77 y codificación Huffman**:

- **LZ77:** busca cadenas de bytes repetidos dentro de una ventana
  de hasta 32 KiB y sustituye repeticiones por pares de
  **longitud + distancia**. No trabaja expresamente con «texturas»:
  busca coincidencias en datos binarios de cualquier tipo.
- **Cadenas hash y búsqueda de coincidencias:** el compresor usa
  índices de secuencias vistas recientemente para localizar posibles
  repeticiones. Buscar más candidatos puede mejorar el tamaño, pero
  incrementa el trabajo de **compresión**.
- **Greedy match (búsqueda rápida):** intenta resolver pronto una
  coincidencia razonable. Reduce el tiempo dedicado a comparar
  alternativas al **crear el PBP**, aunque podría perder oportunidades
  de compresión.
- **Lazy match (evaluación perezosa):** antes de aceptar una coincidencia
  puede avanzar un byte y buscar otra más larga. Esto puede mejorar
  el resultado a costa de más operaciones en el **compresor**.
- **Huffman:** representa los símbolos y las parejas longitud/distancia
  con códigos de longitud variable. El resultado es una secuencia
  DEFLATE que un descompresor compatible puede reconstruir.

### Parámetros exactos del algoritmo que utiliza esta versión

Estos valores proceden de **`DeflaterConstants.cs` de SharpZipLib
1.4.2**, la versión referenciada por `Popstation.csproj`.
No son estimaciones: son los parámetros del **compresor**.

| Nivel | Estrategia | `GOOD_LENGTH` | `MAX_LAZY` | `NICE_LENGTH` | `MAX_CHAIN` |
|:---:|:---|---:|---:|---:|---:|
| **1** | FAST | 4 | 4 | 8 | 4 |
| **2** | FAST | 4 | 5 | 16 | 8 |
| **3** | **FAST** | **4** | **6** | **32** | **32** |
| 4 | FAST | 4 | 4 | 16 | 16 |
| 5 | SLOW | 8 | 16 | 32 | 32 |
| 6 | SLOW | 8 | 16 | 128 | 128 |
| 7 | SLOW | 8 | 32 | 128 | 256 |
| 8 | SLOW | 32 | 128 | 258 | 1024 |
| 9 | SLOW | 32 | 258 | 258 | 4096 |

**Cómo leerlos:**

- `GOOD_LENGTH` y `NICE_LENGTH` son umbrales que influyen en
  cuándo reducir o detener la búsqueda de mejores coincidencias.
- `MAX_LAZY` regula la búsqueda diferida de coincidencias, sobre todo
  en el camino de compresión exhaustivo.
- `MAX_CHAIN` limita el número de candidatos de la cadena hash que
  se inspeccionan **al comprimir**.

Una peculiaridad interesante es que el **nivel 4 sigue siendo FAST**,
pero algunos de sus límites son inferiores a los del **3**
(`MAX_CHAIN`: **16 frente a 32** y `NICE_LENGTH`: **16 frente a 32**).
Por tanto, no tiene por qué producir un archivo más pequeño. El salto
real de **FAST a SLOW ocurre entre los niveles 4 y 5** en SharpZipLib
1.4.2, no entre 3 y 4 como ocurre en determinadas implementaciones de
zlib clásico. Además, los niveles **8 y 9** pueden examinar muchos más
candidatos durante la compresión: **1024 y 4096**, respectivamente.

### ¿Trabaja mucho más la CPU de la PSP al descomprimir un nivel 9?

**No se puede concluir eso solo con la tabla anterior.**
`MAX_CHAIN`, `MAX_LAZY` y las búsquedas greedy/lazy se utilizan
**en el PC que crea el archivo**; no se ejecutan de nuevo en la PSP.

Durante la reproducción, el descompresor interpreta los códigos
Huffman y reconstruye las referencias LZ77. Un flujo creado con nivel
9 **no obliga** al descompresor a intentar 4096 coincidencias; el
nivel numérico no es una orden de búsqueda para la consola.
Todos los niveles utilizan un formato DEFLATE compatible.

**Eso no significa que los tiempos de carga tengan que ser idénticos.**
La secuencia de símbolos producida, los tamaños de los bloques,
la cantidad de datos leídos, la caché, el emulador y la velocidad
de la Memory Stick/microSD pueden influir en las pausas reales.
En un hardware limitado pueden observarse diferencias aunque no
provengan de repetir el trabajo del compresor.

LaNaveRETRO **sí ha observado** diferencias favorables a los niveles
1–3 en determinados juegos sobre PSP original. Lo que **no** afirma
sin una medición adicional es que el nivel 3 consuma un porcentaje
concreto de CPU en descompresión, que 5–9 causen siempre tirones
o que exista un umbral universal de seguridad en el nivel 4.

### Tamaño frente a fluidez: la decisión práctica de LNR

- **1–2:** alternativas razonables para comparar cargas cuando
  se prioriza la respuesta en PSP y se tolera un PBP más grande.
- **3 (predeterminado LNR):** el punto de partida recomendado por la
  **combinación de pruebas de PSP** y de buen compromiso de
  **compresión/tamaño**. La elección no impide cambiarlo.
- **4:** continúa en la vía FAST de SharpZipLib; no garantiza
  mejorar el tamaño ni las cargas frente al 3.
- **5:** nivel histórico predeterminado del proyecto original;
  en algunas pruebas aporta pocos megabytes respecto al 4,
  aunque el resultado depende del juego.
- **6–9:** pueden aumentar considerablemente el esfuerzo para
  **crear** el PBP, sin proporcionar siempre una diferencia de espacio
  que compense. No se afirma que su descompresión sea
  proporcionalmente más costosa.

Los videojuegos ya pueden contener audio, vídeo o recursos
comprimidos; esos datos pueden ser poco reducibles, así que
**no existe un porcentaje universal de ahorro**. Para comparar
niveles deben utilizarse **el mismo juego, la misma imagen de origen,
la misma PSP, emulador y tarjeta** y repetir cada carga.

**Conclusión LNR:** por sus pruebas en PSP real, por el pequeño ahorro
adicional observado con niveles altos en los juegos comparados y por
su comportamiento como compresor rápido, **se recomienda empezar
por el nivel 3**. Es una recomendación **fundamentada y práctica**,
no una afirmación de que la CPU del descompresor quede siempre
«libre de carga» ni un resultado de laboratorio aplicable a todo
el catálogo de PlayStation.

### Fuentes y autoría del análisis

- [Compresión usada en nuestro fork](Popstation/Compression.cs)
  y [referencia a SharpZipLib 1.4.2](Popstation/Popstation.csproj).
- [`DeflaterConstants.cs` de SharpZipLib 1.4.2](https://github.com/icsharpcode/SharpZipLib/blob/v1.4.2/src/ICSharpCode.SharpZipLib/Zip/Compression/DeflaterConstants.cs)
  y [`DeflaterEngine.cs`](https://github.com/icsharpcode/SharpZipLib/blob/v1.4.2/src/ICSharpCode.SharpZipLib/Zip/Compression/DeflaterEngine.cs).
- [RFC 1951: DEFLATE, LZ77, Huffman y búsqueda de coincidencias](https://www.rfc-editor.org/rfc/rfc1951).
- [Documentación de zlib](https://zlib.net/manual.html):
  los parámetros del compresor y los del descompresor no son
  intercambiables.

**Las pruebas de carga en PSP son experiencias propias de
LaNaveRETRO**; el motor PSXPackager es obra de sus autores originales.
Este anexo explica la **decisión del fork LNR**, no representa
una recomendación oficial de RupertAvery ni una modificación del
proyecto original.

---

## Documentacion del proyecto original (conservada)

# PSXPackager

PSXPackager is a tool for converting PlayStation 1 Disc images to the EBOOT.PBP format and back.

EBOOT.PBP is a Sony Playstation file format for PS1 Classics on the PSP and PS3 Playstation emulators. The format is also supported by PS1 emulators such as ePSXe, PCSX, Beetle PSX (Mednafen/RetroArch) and DuckStation.

There is a command-line executable (Windows/Linux/OSX) and a Windows-only GUI available.

The GUI allows you to select and process several images in a queue (Batch mode).

Feel free to take the Popstation library and use it as you like.

The Popstation library was ported from the `popstation-md` C source code to C#.

Looking for the Windows-only GUI README? See [PSXPackagerGUI](PSXPackagerGUI/README.md)

# Download

* [Latest Release](https://github.com/RupertAvery/PSXPackager/releases)

Not sure what to download?

* command line tool - psxpackager-win-x64.zip
* Windows GUI - PsxPackagerGUI.zip

# Features

* Convert .BIN + .CUE or .IMG to .PBP
* Extract .PBP to .BIN + .CUE file 
* Supports conversion of .7z, .zip, .rar files to .PBP
* Supports merging of single-disc, multi-track .BIN + .CUE into one .BIN + .CUE
* Supports writing multi-track CUE information to PBP for audio 
* PBP Compression levels from 0 to 9
* Supports writing multi-disc PBP using .m3u files
* Supports extracting multi-disc PBP
* Windows/Linux/OSX CLI
* GUI with PSX2PSP-like interface and batch processing

# Requirements

* PSXPackager requires the [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
* PSXPackagerGUI requires the [.NET Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

# Usage

The basic usage of PSXPackager accepts a file or path to convert.

```
psxpackager -i <input_path_or_file> [-o <output_path_or_file>]
```

The `-i` or `input` parameter is required. It specifies a path to a file, or a directory and a wildcard expression. If a wildcard expression is used, it will process all matching files in the directory.

The application will assume that you want to convert any image to a PBP, and any PBP to a .BIN + .CUE.

The command:

```
psxpackager -i "Final Fantasy VII - Disc 1.cue"
```

will process `Final Fantasy VII - Disc 1.bin` and `Final Fantasy VII - Disc 1.cue` and output `Final Fantasy VII - Disc 1.pbp`.


The `-o` or `output` parameter is optional. It specifies the folder where the the converted or extracted files will be placed. If not specified, the folder specified in the input is used.

If the input is a file and the output is also a file, the output will be renamed, instead of reusing the input filename.

## Options

```
  -i, --input                (Group: input) The input file or path to convert. The filename may contain wildcards.

  -o, --output               The output path where the converted file(s) will be written.

  -l, --level                (Default: 3) Set compression level 0-9, default 3 in PSXPackagerLNR.

  -r, --recursive            Recurse subdirectories

  -d, --discs                A comma-separated list of disc numbers to extract from a PBP.

  -v, --verbosity            (Default: 3) Set level of output messages. 1 = Files, Errors and Warnings only, 2 = No
                             Info-level messages, 3 = All messages (default), 4 = Include timestamps

  -x                         If specified, overwrite a file if it exists, otherwise ask confirmation.

  -s, --skip                 If specified, will skip existing files.

  -f, --format               (Default: %FILENAME%) Specify the filename format e.g. [%GAMEID%] [%MAINGAMEID%] %TITLE%
                             (%REGION%) or %FILENAME%

  -g, --log                  If specified, log messages to a file.

  --extract                  If specified, extract resources using the path specified by resource-format. See README for
                             more details.

  --import                   If specified, import resources using the path specified by resource-format. See README for
                             more details.

  --generate                 If specified, create empty resources folder specified by resource-format. See README for
                             more details.

  --resource-format          The format to use with extract/import/generate. See README for more details.

  --resource-root            The path where resource folders will be located. If not specified, the path will be the
                             same as the input file

  --help                     Display this help screen.

  --version                  Display version information.
```

## Convert a .BIN, .CUE, .ISO, .IMG, .CHD or archive to a .PBP

PSXPackager supports several input formats. Simply pass the path to the archive, CUE sheet, or image with the `-i` parameter.

```
psxpackager -i <path_to_file> [-o <output_path>] [-l <compression_level>] [-y]
```

The output path is optional. If not specified, the path of the input file will be used.

PSXPackager will prompt if a file exists before overwriting it. Use the `-x` argument to overwrite all files in the output directory.

Set the compression level to a value from 0 to 9, with 0 being no compression and 9 being the highest compression level. **This PSXPackagerLNR fork defaults to 3** (the original upstream PSXPackager defaults to 5).

PSXPackager can automatically extract files from compressed files (archives) and convert the contents into a PBP. The following formats are supported:

* .zip
* .7z
* .rar
* .tar
* .gz

Archives will be decompressed to a temporary folder in `%TEMP%\PSXPackager`, and will be cleaned up on exit.

### Sector formats

PlayStation discs store 2352 bytes per sector, and a PBP expects the disc data in that raw form. Images come in two flavours:

* **Raw** images (usually `.bin`, sometimes `.img`) hold the complete 2352-byte sectors.
* **Cooked** images (usually `.iso`) hold only the 2048 bytes of user data per sector, discarding the sync pattern, header, subheader and the EDC/ECC error correction fields.

PSXPackager works out which one you have from the contents of the file rather than from its extension, so a `.iso` that is really a raw image, and a `.bin` that is really cooked, are both handled correctly. A cooked image is expanded back to Mode 2 Form 1 sectors as it is read, regenerating the sync pattern, the MSF header, the subheader and the EDC/ECC fields.

> **Prefer `.bin` / `.cue` where you have the choice.** A cooked `.iso` cannot store CD-XA subheaders, and an XA sector holds 2324 bytes of data where a `.iso` keeps only 2048. Streaming audio and FMV therefore cannot be rebuilt from a `.iso`, and games that use them will lose that audio and video. PSXPackager warns you when it converts a cooked image. The game data itself is reconstructed exactly.

### CHD images

A `.chd` (Compressed Hunks of Data) holds a whole disc, audio tracks and all, in one compressed file. PSXPackager reads them directly - there is no need to run `chdman` first, and nothing is written out to a temporary `.bin`.

The disc is rebuilt as it is read: the padding CHD inserts between tracks is skipped, pregaps that were never stored are regenerated, and audio, which a CHD holds in the opposite byte order from a `.bin`, is swapped back. The track list comes from the CHD's own metadata, so a game with CD audio gets a PBP with the right table of contents without you supplying a cue sheet.

Versions 3, 4 and 5 are supported, with the `zlib`, `lzma`, `zstd`, `cdzl`, `cdlz`, `cdfl` and `cdzs` codecs. Two things are not supported:

* **Delta CHDs**, which store only the difference from a parent file. PSXPackager will tell you if it meets one.
* **Subcode data**, which is discarded. A PBP has nowhere to keep it.

CHDs made from a `.bin` / `.cue` are lossless, so the PBP you get is the same one you would get by extracting the CHD to a `.bin` / `.cue` first. A CHD made from a `.iso` carries the same limitation the `.iso` did.

## Extract a .PBP to a .BIN + .CUE

PSXPackager checks the file extension to decide whether to extract or convert, so the syntax remains the same. The output will always be a `.bin` + `.cue`.

Extracting from Multi-disc PBPs are supported. Specify the discs to extract with the `-d` or `--disc` option, which takes a comma-separated list of disc numbers to extract, e.g. `-d 1,2`. If this option is not specified, all discs will be extracted.

```
psxpackager -i <path_to_pbp> [-o <output_path>] [-d <list_of_discs>]
```

## Filename formatting

Use the `-f` or `format` option to specify the format of the output filename based on the information found on the disc (Sony GameID) and the `gameInfo.db` file.

This allows you to generate standardized output filenames that can include the GameID e.g SCUS-94163, the region (NTSC/PAL) and the game name.  

By default, it will use `%FILENAME%`, i.e. the input filename as the output filename.

See [Formatting](#formatting) for more info.

## Merging Multi-Track Games

Some games such as Tomb Raider have additional digital audio tracks on the CD, for use as background music.

These audio tracks are usually extracted by disc rippers as separate `.BIN` files. They usually are named `<Game Name> - Track nn.bin` or similar, where `nn` is a 2-digit number. They usually come with a `.CUE` file which is an index for all the related `.BIN` files.

Usually you would need to use CDMage or some other tool to merge multi-track discs.

With PSXPackager, all you need to do is pass in the `.CUE` file as the input file and it will automatically merge multi-track discs before converting to PBP as long as  

The resulting PBP file will contain the audio tracks as disc tracks and you will be able to play your game on your PSP or emulator with music.

## Creating Multi-disc PBPs

The PBP format can contain more than one disc, and this is supported by PSXPackager. This allows you to merge a multi-disc game such as Final Fantasy VII (3 discs) into a single PBP. 

To do this from the command line, first create an `.m3u` file containing a list of the discs in the order you wish them to appear in the PBP.

For example, create a text file with the following contents and save as `Final Fantasy VIII.m3u`

```
Final Fantasy VIII - Disc 1.cue
Final Fantasy VIII - Disc 2.cue
Final Fantasy VIII - Disc 3.cue
```

Call `psxpackager` with the `.m3u` as the input file.

```
psxpackager -i "Final Fantasy VIII.m3u" [-o <output_path>]
```

The file  `Final Fantasy VIII.PBP` will be created which contains the three discs in one file.

## Batch Conversion

Wildcards are now supported using the `-i` argument

```
psxpackager -i <path_containing_files>\<wildcard_filter> [-o <output_path>]
```

This will process all supported files in the folder `C:\Roms`. Use with caution, as you may be overwriting files that exist.

```
psxpackager -i "C:\Roms\*.*"
```

This will extract all PBP files in the folder `C:\Roms` to BIN+CUE.

```
psxpackager -i "C:\Roms\*.PBP"
```

This will convert all files matching `Legend of Dragoon - Disc ?.bin` in the folder `C:\Roms` to PBP.

```
psxpackager -i "C:\Roms\Legend of Dragoon - Disc ?.bin"
```

If you want faster batch conversion, use the GUI, as it supports multi-threaded processing.

# Customizing PBPs with Resources

Resource files are PBP-specific embedded resources that are normally used by the PSP, PSVita or PS3 to display an image or play audio on the XMB when the game is selected. These files usually named ICON0.PNG, PIC0.PNG, PIC1.PNG and SND0.AT3.

PSXPackager can extract (`--extract`) or embed (`--import`) resource files for a single conversion or batch conversion. To do the latter, the resource files must be located in a specific location.

There are two options that let you set how and where the resource files are found.

The `--resource-format` option lets you match a folder based on filename, gameid and other formats. See [Formatting](#formatting). omitting this option defaults the format to `%FILENAME%`, meaning the input filename without the extension will be used.

The `--resource-root` option sets the root path of your resource folders. This allows you to separate your disc images from your resource folders.  You can omit this option to use the same folder as the input file as the root path of your resource folder.

## Extract Resources

Extracts resources from a PBP.

```
psxpackager -i <path_to_file> --extract [--resource-format <resource format>] [--resource-root <resource root folder>] [-o <output_path>]
```

`--resource-format` - the format of the folder name to extract the embedded resources to. See [Formatting](#formatting).  If not specified, the default value will be `%FILENAME%\%RESOURCE%.%EXT%`

`--resource-root` - the root folder where to place the resource folders. Do not specify to place the resource folder next to the input file.

For example, if you extract the resources from a `Final Fantasy VII (DISC 1).PBP` file using the commande below:

```
psxpackager -i "Final Fantasy VII (DISC 1).PBP" --extract --resource-path %GAMEID%\%RESOURCE%.%EXT%
```

You would have extracted the following files:

```
SCUS-94163\ICON0.PNG
SCUS-94163\PIC0.PNG
SCUS-94163\PIC1.PNG
```

## Import Resources

Imports custom resources into PBP.

```
psxpackager -i <path_to_file> --import [--resource-format <resource format>] [--resource-folder <resource root folder>] [-o <output_path>]
```

`--resource-format` - the format of the folder name to import the embedded resources from. See [Formatting](#formatting).  If not specified, the default value will be `%FILENAME%\%RESOURCE%.%EXT%`

`--resource-root` - the root folder where to start searching for the resource folders. Do not specify to look for the resource folder next to the input file.

For example, if you want to build a PBP from a BIN+CUE file and you have the resources next to the file in a folder such as below:

```
Final Fantasy VII (Disc-1).bin
Final Fantasy VII (Disc-1).cue
SCUS-94163\ICON0.PNG
SCUS-94163\PIC0.PNG
SCUS-94163\PIC1.PNG
```

You can build your PBP using the following command:

```
psxpackager -i "Final Fantasy VII (Disc-1).cue" --import --resource-path %GAMEID%\%RESOURCE%.%EXT%
```

## Generate Resource Folders

This option is provided for the following scenario:

You have a bunch of images you want to convert to PBP, and you want to customize all of them.  To do this, you will need to generate resource folders, where you will later place the icons and backgrounds you want.

This option will create empty folders using the specified format.

```
psxpackager -i <path_to_file> --generate [--resource-format <resource format>] [--resource-folder <resource root folder>]
```

`--resource-format` - the format of the folder name to generate. See [Formatting](#formatting).  If not specified, the default value will be `%FILENAME%`

For example, this will generate the empty folder `SCUS-94163`:

```
psxpackager -i "Final Fantasy VII (Disc-1).cue" --generate --resource-path %GAMEID%
```

# Formatting

```
%FILENAME%   - The input filename
 
%GAMEID%     - The GAMEID of the disc. For multi-disc games, each disc will have a differnt GAMEID. 

%MAINGAMEID% - The GAMEID of the first disc in a multi-disc game.

%TITLE%      - The Disc Title of the game. This will contain the Disc number or other identifier in a mult-disc game.

%MAINTITLE%  - The Main title of the game. This will be the actual title of the game.

%REGION%     - The game region, i.e. NTSC or PAL.

%RESOURCE%   - The resource type. (ICON0, ICON1, PIC0, PIC1, SND0). Only used for resource files

%EXT%        - The resource extension. (PNG, AT3). Only used for resource files

```

For example, processing `Final Fantasy VIII - Disc 1.iso` with the following format:

```
[%GAMEID%] %TITLE% (%REGION%)
```

will generate the filename

```
[SLUS00892] Final Fantasy VIII - Disc 1 (NTSC).pbp
```

Note that when extracting from a multi-disc PBP PSXPackager will append the disc number to the file format.


# FAQ

### Does PSXPackager support audio tracks? 

Yes, but only as the original audio (no compression).

### How can I convert a game with audio tracks into a PBP? 

If you have a `.CUE` file with multiple `.BIN` files, pass the `.CUE` file as the input file and PSXPackager will automatically merge the `.BIN` files into a single file in a temporary folder.

A new CUE sheet will also be created with all tracks under the merged `.BIN`, and index positions will be updated.

This merged `.CUE` file will be used to create a TOC (Table of Contents) for the PBP ISO. This allows audio tracks to be correctly read from the PBP.

Temporary files will be deleted when conversion is complete, if the conversion is cancelled, or if an error occurs.

The temporary folder location is `%TEMP%\PSXPackager`.

# Buy me a beer?

Hey programming is fun, but it's also tiring. I mean I could have just been drinking a beer. If you saved yourself a week of converting stuff, why not consider...

* [Buy me a nice beer](https://www.paypal.me/rupertavery/5.00?locale.x=en_US)
* [Buy me a good beer](https://www.paypal.me/rupertavery/2.50?locale.x=en_US)
* [Buy me a decent beer](https://www.paypal.me/rupertavery/1.50?locale.x=en_US)

and I will raise one to you! Much appreciated!
