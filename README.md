# PSXPackagerLNR - LaNaveRETRO Edition (fork)

Esta es una **adaptacion y automatizacion comunitaria** del proyecto original
[PSXPackager de RupertAvery](https://github.com/RupertAvery/PSXPackager).
**No es un programa creado originalmente por LaNaveRETRO**. Se conservan
la licencia MIT y los creditos del software original.

## Versionado y descargas identificables

La version propia de LaNaveRETRO comienza en **v0.1.0-beta.1**.
La version se define en [LNR/VERSION](LNR/VERSION) y sus novedades
se documentan en [CHANGELOG-LNR.md](CHANGELOG-LNR.md).

**Cada compilacion de GitHub Actions lleva ademas un numero irrepetible**.
Por ejemplo: `PSXPackagerLNR-v0.1.0-beta.1-b59-GUI-win-x64.zip`.
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
2. Extrae `PSXPackagerLNR-GUI-win-x64.zip` completo en una carpeta
   de Windows (no ejecutes el programa directamente dentro del ZIP).
3. Inicia `PSXPackagerGUI.exe` y pulsa el nuevo menu
   **CHD a PBP - LNR**.
4. Selecciona la carpeta que contiene los CHD. Elige **separados** o
   **unidos** y nivel **1, 5 o 9** (5 es el predeterminado).
5. Pulsa **Analizar**, revisa el plan y despues **Convertir**.
   Los PBP apareceran en la subcarpeta `PBP`. Los originales no
   se modifican y los destinos existentes se omiten.

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

**Validado por GitHub Actions:** compila la GUI/CLI y supera pruebas
del planificador del BAT. **Pendiente:** probar conversiones reales
en Windows desde la nueva ventana, pistas CD-Audio y cambio de disco.

- [Instrucciones del Toolkit](LNR/Batch/README.md)
- [Compilaciones Windows y artefactos ZIP](https://github.com/PaPer-DJ/PSXPackagerLNR/actions)
- [Solicitud de cambios en desarrollo](https://github.com/PaPer-DJ/PSXPackagerLNR/pull/1)

El BAT ofrece discos **separados** (nombres exactos) o **unidos** (M3U) y
niveles de compresion **1, 5 y 9**, con el nivel 5 como opcion normal.
Los juegos, BIOS y archivos protegidos por derechos de terceros **no se
distribuyen** con el proyecto.

> Nota: una conversion PBP correcta no garantiza que todos los emuladores
> admitan audio CD, SBI o cambios de disco de la misma manera. Comprueba
> cada plataforma. Los ejecutables siguen conservando los nombres originales
> durante esta fase de validacion.

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

  -l, --level                (Default: 5) Set compression level 0-9, default 5.

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

Set the compression level to a value from 0 to 9, with 0 being no compression and 9 being the highest compression level. If not specified, it will default to 5.

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
