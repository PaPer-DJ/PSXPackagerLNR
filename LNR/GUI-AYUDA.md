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

## 2. Seleccionar carpeta y analizar

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

## 4. Compresión PBP

- **1 — Ligera**: compresión baja; útil como opción de comparación.
- **3 — Equilibrada (LNR)**: opción inicial recomendada por esta edición
  para comenzar pruebas en dispositivos modestos.
- **5 — Estándar original**: nivel predeterminado del PSXPackager original.
- **9 — Máxima**: prioriza el tamaño y normalmente tarda más en comprimir.

**Importante:** no hay evidencia suficiente para asegurar que el nivel 3 sea
siempre más rápido de descomprimir que el 5. El resultado y la fluidez
dependen del juego, emulador, CPU, tarjeta y ajustes. Una compresión más alta
**no** produce necesariamente siempre un PBP más pequeño.

Las nuevas instalaciones de LNR utilizan 3. Si un usuario ya guardó una
preferencia en una beta anterior, se respeta su selección previa. No se
cambia el nivel predeterminado del motor CLI original.

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
**Abrir carpeta PBP** abre los resultados. Puedes ampliar el registro
arrastrando el separador y desmarcar **Seguir actividad** para consultarlo.

## 6. Eliminar CHD convertidos — función avanzada

**Desactivada en cada inicio y en cada lote; nunca se recuerda activada.**

Al activarla se muestra una **confirmación adicional** de borrado
irreversible. Si la aceptas, tras **cada conversión nueva** que haya
terminado correctamente, la aplicación:

1. Comprueba que se generó un único PBP, que tenga longitud mínima y
   que empiece por la firma reconocible `00 50 42 50`.
2. Mueve el PBP temporal a su ubicación final en la misma unidad y
   comprueba su tamaño y firma una vez colocado.
3. Conserva los archivos auxiliares M3U y SBI. Si falla la copia de
   algún SBI que corresponde al juego, no elimina sus CHD.
4. Comprueba que el CHD original no ha cambiado desde el análisis y
   que no es un enlace simbólico, y **solo entonces intenta borrarlo**.

**No** borra CHD de PBP que ya existían, conversiones fallidas o
canceladas. El borrado usa eliminación directa de archivos: **no pasa
por la Papelera de Windows**. Si no puedes permitirte perder los
originales, haz copia de seguridad y deja la casilla desactivada.

**Limitación:** comprobar tamaño y cabecera del PBP no equivale a probar
todos sus sectores ni a verificar que la consola/emulador reproduce
correctamente el juego. La compatibilidad y el audio deben comprobarse
de forma práctica. En juegos multidisco se podrían borrar varios CHD
solo después de crear un PBP unido nuevo; asegúrate de que tu emulador
admite el cambio de disco antes de activar el borrado.

### Espacio en disco: lo que sí y lo que no evita

La conversión CHD→PBP **no** crea imágenes BIN/CUE intermedias, pero
debe escribir el **PBP completo** antes de poder borrar el CHD de ese
juego. Por ello el espacio libre debe bastar, al menos, para el siguiente
PBP. Para reducir la ocupación máxima, el PBP provisional se escribe en
una carpeta temporal **dentro de `PBP` en el mismo volumen** y luego se
renombra a su ubicación definitiva sin copiarlo íntegramente de nuevo.
Tras borrar cada CHD exitoso, el espacio se va liberando para el siguiente.

No garantiza poder convertir un CHD si el disco está completamente lleno
o no queda suficiente espacio para producir su PBP.

## 7. Conversión inversa y formatos de disco: alcance de la suite

La edición LNR se está ampliando para admitir también:

| Dirección | Tecnología | Limitaciones |
|---|---|---|
| PBP → BIN/CUE | Extracción disponible en PSXPackager | PS1; multidisco produce discos separados |
| PBP → CHD | Extraer BIN/CUE y comprimir con CHDMAN | Se necesita espacio temporal para BIN/CUE; no es reversión bit a bit garantizada |
| CHD de CD → BIN/CUE | `chdman extractcd` | PS1, Saturn y PS2 en CD; incluye pistas de audio cuando están preservadas |
| CHD de DVD → ISO | `chdman extractdvd` | PS2 en DVD; no usar BIN/CUE de CD por defecto |

Las operaciones con CHDMAN necesitan el ejecutable oficial de MAME
y están **separadas** de la función directa de CHD→PBP. La documentación
de CHDMAN está en <https://docs.mamedev.org/tools/chdman.html>.

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
