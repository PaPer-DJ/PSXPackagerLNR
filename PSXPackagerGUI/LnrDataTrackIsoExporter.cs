using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Exports the user-data portion of a *single data track* CD image.
    /// Not a disc-preservation format: CD-DA, XA Form 2, subchannel,
    /// and non-2048-byte sectors cannot be represented as plain ISO.
    /// </summary>
    public static class LnrDataTrackIsoExporter
    {
        private const int RawSectorSize = 2352;
        private const int UserDataSize = 2048;

        private static readonly Regex FileLine = new(
            "^\\s*FILE\\s+(?:\"(?<name>[^\"]+)\"|(?<name>[^\\r\\n]+?))\\s+BINARY\\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex TrackLine = new(
            "^\\s*TRACK\\s+\\d+\\s+(?<mode>\\S+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex IndexLine = new(
            "^\\s*INDEX\\s+(?<number>\\d+)\\s+(?<position>\\d{2}:\\d{2}:\\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static async Task<long> ExportAsync(
            string cuePath, string destinationIso, CancellationToken cancellationToken)
        {
            var fileName = "";
            var mode = "";
            var tracks = 0;
            var files = 0;
            var indices = 0;
            var indexIsZero = false;

            foreach (var line in File.ReadAllLines(cuePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = FileLine.Match(line);
                if (file.Success)
                {
                    files++;
                    fileName = file.Groups["name"].Value.Trim();
                }

                var track = TrackLine.Match(line);
                if (track.Success)
                {
                    tracks++;
                    mode = track.Groups["mode"].Value.ToUpperInvariant();
                }

                var index = IndexLine.Match(line);
                if (index.Success)
                {
                    indices++;
                    indexIsZero = index.Groups["number"].Value == "01" &&
                                  index.Groups["position"].Value == "00:00:00";
                }

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("PREGAP ", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("POSTGAP ", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "El CUE contiene pausas; usa PBP → BIN/CUE para conservar la estructura del CD.");

                // Reject unrecognized FILE/TRACK/INDEX declarations rather than
                // accidentally treating an extra disc or track as data only.
                if ((trimmed.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase) && !file.Success) ||
                    (trimmed.StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase) && !track.Success) ||
                    (trimmed.StartsWith("INDEX ", StringComparison.OrdinalIgnoreCase) && !index.Success))
                    throw new InvalidDataException(
                        "El CUE tiene una declaración no compatible con ISO. Usa BIN/CUE.");
            }

            if (files != 1 || tracks != 1 || indices != 1 || !indexIsZero)
                throw new InvalidDataException(
                    "Solo es posible extraer ISO de un único disco con una sola pista de datos, sin pausas. " +
                    "Elige PBP → BIN/CUE para juegos multidisco o con CD-Audio.");

            if (mode != "MODE1/2352" && mode != "MODE2/2352" && mode != "MODE1/2048")
                throw new InvalidDataException(
                    "La pista no es un CD de datos compatible con ISO de 2048 bytes. " +
                    "Elige PBP → BIN/CUE para conservar el disco completo.");

            if (fileName != Path.GetFileName(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El CUE referencia un BIN externo o no válido.");

            var folder = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
            var bin = Path.Combine(folder, fileName);
            if (!File.Exists(bin))
                throw new FileNotFoundException("El BIN indicado por el CUE no existe.", bin);

            await using var input = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read,
                                                    128 * 1024, useAsync: true);
            var raw = mode != "MODE1/2048";
            var sectorSize = raw ? RawSectorSize : UserDataSize;
            if (input.Length < sectorSize * 16L || input.Length % sectorSize != 0)
                throw new InvalidDataException("La imagen contiene sectores incompletos.");

            await using var output = new FileStream(destinationIso, FileMode.CreateNew, FileAccess.Write,
                                                     FileShare.None, 128 * 1024, useAsync: true);
            var sector = new byte[sectorSize];
            long sectors = input.Length / sectorSize;
            for (long i = 0; i < sectors; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await input.ReadExactlyAsync(sector.AsMemory(), cancellationToken);
                var offset = 0;
                if (raw)
                {
                    // 12-byte sync header must be intact.
                    if (sector[0] != 0 || sector[11] != 0 ||
                        sector.Skip(1).Take(10).Any(b => b != 0xFF))
                        throw new InvalidDataException(
                            $"Sector {i}: cabecera de CD no válida. No se generará un ISO incompleto.");

                    var sectorMode = sector[15];
                    if (mode == "MODE1/2352" && sectorMode == 1)
                    {
                        offset = 16;
                    }
                    else if (mode == "MODE2/2352" && sectorMode == 2 &&
                             sector[18] == sector[22] &&
                             sector[16] == sector[20] && sector[17] == sector[21] &&
                             sector[19] == sector[23] &&
                             (sector[18] & 0x20) == 0)
                    {
                        offset = 24; // XA Form 1 only; Form 2 (2324 bytes) is unsafe.
                    }
                    else
                        throw new InvalidDataException(
                            $"Sector {i}: audio, XA Form 2 o tipo de sector no compatible. " +
                            "Usa PBP → BIN/CUE; ISO perdería información.");
                }
                await output.WriteAsync(sector.AsMemory(offset, UserDataSize), cancellationToken);
            }
            await output.FlushAsync(cancellationToken);
            return sectors;
        }
    }
}