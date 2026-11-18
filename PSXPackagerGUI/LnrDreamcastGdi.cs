using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Read-only checks for Dreamcast GD-ROM .gdi descriptors and CHD GD-ROM
    /// metadata reported by MAME CHDMAN. Does not create, fix or alter tracks.
    /// </summary>
    internal static class LnrDreamcastGdi
    {
        private static readonly Regex TrackLine = new(
            @"^\s*(?<track>\d+)\s+(?<lba>\d+)\s+(?<type>\d+)\s+(?<size>\d+)\s+(?<file>""[^""]+""|.+?)\s+(?<offset>\d+)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex GdromMetadata = new(
            @"\b(?:CHGD|CHGT)\b", RegexOptions.Compiled |
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        internal sealed record Track(int Number, long Lba, int Type, int SectorSize,
                                     string FilePath, long Offset);

        internal sealed record Disc(IReadOnlyList<Track> Tracks, long InputBytes);

        internal static bool IsGdromMetadata(string chdmanInfo) =>
            !string.IsNullOrEmpty(chdmanInfo) && GdromMetadata.IsMatch(chdmanInfo);

        internal static Disc Validate(string descriptor)
        {
            var source = Path.GetFullPath(descriptor);
            if (!source.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("La fuente no es un descriptor .gdi.");

            var lines = File.ReadAllLines(source);
            if (lines.Length < 4 ||
                !int.TryParse(lines[0].Trim().TrimStart('\uFEFF'),
                    NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                count is < 3 or > 99)
                throw new InvalidDataException(
                    "Cabecera GDI inválida: se esperaba un número de pistas entre 3 y 99.");

            var records = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            if (records.Length != count)
                throw new InvalidDataException(
                    $"GDI incompleto: declara {count} pistas y contiene {records.Length} líneas.");

            var root = Path.GetDirectoryName(source)
                ?? throw new InvalidDataException("GDI sin directorio de origen.");
            var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            var tracks = new List<Track>();
            var uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long size = new FileInfo(source).Length;
            long previousLba = -1;

            foreach (var row in records)
            {
                var match = TrackLine.Match(row);
                if (!match.Success ||
                    !int.TryParse(match.Groups["track"].Value, NumberStyles.None,
                                  CultureInfo.InvariantCulture, out var number) ||
                    !long.TryParse(match.Groups["lba"].Value, NumberStyles.None,
                                   CultureInfo.InvariantCulture, out var lba) ||
                    !int.TryParse(match.Groups["type"].Value, NumberStyles.None,
                                  CultureInfo.InvariantCulture, out var type) ||
                    !int.TryParse(match.Groups["size"].Value, NumberStyles.None,
                                  CultureInfo.InvariantCulture, out var sectorSize) ||
                    !long.TryParse(match.Groups["offset"].Value, NumberStyles.None,
                                   CultureInfo.InvariantCulture, out var offset))
                    throw new InvalidDataException("Línea de pista GDI no reconocida: " + row);

                if (number != tracks.Count + 1 || lba <= previousLba ||
                    type is not (0 or 4) ||
                    sectorSize is not (2048 or 2336 or 2352) || offset < 0)
                    throw new InvalidDataException(
                        $"Pista GDI {number}: numeración, posición, tipo o tamaño de sector incorrectos.");

                var fileName = match.Groups["file"].Value.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
                    throw new InvalidDataException("El GDI contiene una ruta absoluta o vacía.");

                var filePath = Path.GetFullPath(Path.Combine(root, fileName));
                if (!filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("La pista GDI intenta salir de la carpeta origen: " + fileName);
                if (!File.Exists(filePath))
                    throw new InvalidDataException("Falta una pista GDI: " + fileName);

                var file = new FileInfo(filePath);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("La pista GDI es un enlace y no se seguirá: " + fileName);
                var parent = file.Directory;
                while (parent != null &&
                    parent.FullName.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("La pista GDI usa una carpeta enlazada: " + fileName);
                    parent = parent.Parent;
                }
                if (offset >= file.Length || file.Length - offset < sectorSize ||
                    (file.Length - offset) % sectorSize != 0)
                    throw new InvalidDataException(
                        "Pista GDI truncada o con sectores incompletos: " + fileName);

                tracks.Add(new Track(number, lba, type, sectorSize, filePath, offset));
                if (uniqueFiles.Add(filePath))
                    size = checked(size + file.Length);
                previousLba = lba;
            }

            if (!tracks.Any(track => track.Type == 4 && track.Lba >= 45000))
                throw new InvalidDataException(
                    "No se ha encontrado la zona de alta densidad del GD-ROM de Dreamcast.");
            return new Disc(tracks, size);
        }
    }
}
