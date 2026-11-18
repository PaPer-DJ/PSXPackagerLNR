using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Disc-index-safe naming for the Suite. The upstream extractor names
    /// multidisc output "NAME - Disc N.cue"; our published filenames use
    /// "NAME (Disc N)" consistently. Each CHD is still a single disc.
    /// This helper has no WPF, CHDMAN, or commercial-game dependency.
    /// </summary>
    public static class LnrSuiteDiscNaming
    {
        private static readonly Regex ExistingDiscSuffix = new(
            @"(?:\s*\(Disc\s+\d+\)|\s+-\s+Disc\s+\d+)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex ExtractedDiscSuffix = new(
            @"\s+-\s+Disc\s+(?<number>\d+)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex CueFileLine = new(
            "^(?<start>\\s*FILE\\s+)(?:\\\"(?<quoted>[^\\\"]+)\\\"|(?<plain>[^\\r\\n]+?))(?<end>\\s+BINARY\\s*)$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);
        private static readonly Regex AnyCueFileLine = new(
            @"^\s*FILE\s+",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);

        public static string GameName(string source)
        {
            var stem = Path.GetFileNameWithoutExtension(source);
            return string.IsNullOrWhiteSpace(stem)
                ? throw new InvalidDataException("Nombre de origen sin juego.")
                : stem;
        }

        public static string DiscName(string source, int count, int index)
        {
            if (count is < 1 or > 5 || index < 1 || index > count)
                throw new InvalidDataException("Índice o cantidad de discos no válidos.");
            var game = GameName(source);
            if (count == 1) return game;
            var stripped = ExistingDiscSuffix.Replace(game, "").TrimEnd();
            if (stripped.Length == 0) throw new InvalidDataException("Título de juego vacío.");
            return stripped + " (Disc " + index + ")";
        }

        public static IReadOnlyList<string> ChdNames(string source, int count) =>
            Enumerable.Range(1, count).Select(i => DiscName(source, count, i) + ".chd").ToArray();

        public static IReadOnlyList<string> ExtractedCues(string stage, int count)
        {
            var cues = Directory.GetFiles(stage, "*.cue", SearchOption.TopDirectoryOnly);
            if (count is < 1 or > 5 || cues.Length != count)
                throw new InvalidDataException(
                    "El PBP no produjo un archivo CUE por disco: esperados " + count +
                    "; encontrados " + cues.Length + ".");
            if (count == 1) return cues;
            var byIndex = new Dictionary<int, string>();
            foreach (var cue in cues)
            {
                var match = ExtractedDiscSuffix.Match(Path.GetFileNameWithoutExtension(cue));
                if (!match.Success ||
                    !int.TryParse(match.Groups["number"].Value, out var index) ||
                    index < 1 || index > count || !byIndex.TryAdd(index, cue))
                    throw new InvalidDataException(
                        "No se pudo comprobar el orden de discos del PBP: " +
                        Path.GetFileName(cue));
            }
            return Enumerable.Range(1, count).Select(i => byIndex[i]).ToArray();
        }

        /// <summary>
        /// Rename the extracted CUE and BIN files by disc index. Update EVERY
        /// CUE FILE reference (including several track references to one BIN)
        /// and reject unsupported/unresolved references before publishing.
        /// </summary>
        public static void NormalizeBinCue(string stage, string source, int count)
        {
            var cues = ExtractedCues(stage, count);
            var referencedBins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plans = new List<(string originalCue, string newCue, string text,
                                  List<(string oldBin, string newBin)> bins)>();
            for (var i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                var name = DiscName(source, count, i + 1);
                var text = File.ReadAllText(cue);
                var matches = CueFileLine.Matches(text);
                if (matches.Count == 0 || matches.Count != AnyCueFileLine.Matches(text).Count)
                    throw new InvalidDataException("CUE sin archivos BIN válidos: " +
                                                   Path.GetFileName(cue));

                var oldNames = matches.Cast<Match>()
                    .Select(m => m.Groups["quoted"].Success
                        ? m.Groups["quoted"].Value : m.Groups["plain"].Value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var b = 0; b < oldNames.Length; b++)
                {
                    var oldName = oldNames[b];
                    if (oldName != Path.GetFileName(oldName) ||
                        !oldName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
                        !referencedBins.Add(oldName))
                        throw new InvalidDataException("BIN compartido, repetido o inseguro: " + oldName);
                    var oldPath = Path.Combine(stage, oldName);
                    if (!File.Exists(oldPath) || new FileInfo(oldPath).Length < 2048)
                        throw new InvalidDataException("BIN ausente o incompleto: " + oldName);
                    var suffix = oldNames.Length > 1 ? " - Track " + (b + 1) : "";
                    renames.Add(oldName, name + suffix + ".bin");
                }

                var fixedCue = CueFileLine.Replace(text, match =>
                {
                    var oldName = match.Groups["quoted"].Success
                        ? match.Groups["quoted"].Value : match.Groups["plain"].Value.Trim();
                    return match.Groups["start"].Value + "\"" + renames[oldName] + "\"" +
                           match.Groups["end"].Value;
                });
                plans.Add((cue, Path.Combine(stage, name + ".cue"), fixedCue,
                    renames.Select(p => (Path.Combine(stage, p.Key),
                                         Path.Combine(stage, p.Value))).ToList()));
            }

            var allBins = Directory.GetFiles(stage, "*.bin", SearchOption.TopDirectoryOnly);
            if (allBins.Length != referencedBins.Count)
                throw new InvalidDataException("El PBP generó BIN sin referencia CUE: se cancela la publicación.");
            var finalFiles = plans.Select(p => p.newCue)
                .Concat(plans.SelectMany(p => p.bins.Select(b => b.newBin))).ToArray();
            if (finalFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != finalFiles.Length)
                throw new InvalidDataException("Colisión de nombres entre discos extraídos.");

            // All work is inside a throwaway staging folder. Failures never
            // affect pre-existing outputs or original PBP files.
            foreach (var plan in plans)
                foreach (var bin in plan.bins)
                    if (!string.Equals(bin.oldBin, bin.newBin, StringComparison.OrdinalIgnoreCase))
                        File.Move(bin.oldBin, bin.newBin);
            foreach (var plan in plans)
            {
                if (!string.Equals(plan.originalCue, plan.newCue,
                                   StringComparison.OrdinalIgnoreCase))
                    File.Delete(plan.originalCue);
                File.WriteAllText(plan.newCue, plan.text, new UTF8Encoding(false));
            }
            // Verify the FINAL cue references after renaming, not just the
            // original extraction's references.
            foreach (var plan in plans)
            {
                var fixedText = File.ReadAllText(plan.newCue);
                var matches = CueFileLine.Matches(fixedText);
                if (matches.Count == 0 ||
                    matches.Count != AnyCueFileLine.Matches(fixedText).Count)
                    throw new InvalidDataException("CUE renombrado inválido.");
                foreach (Match match in matches)
                {
                    var filename = match.Groups["quoted"].Success
                        ? match.Groups["quoted"].Value : match.Groups["plain"].Value.Trim();
                    if (filename != Path.GetFileName(filename) ||
                        !File.Exists(Path.Combine(stage, filename)))
                        throw new InvalidDataException("El CUE final no referencia un BIN existente.");
                }
            }
        }

        /// <summary>
        /// Publish the entire multidisc CHD group, or none. Caller must have
        /// verified each staged CHD and chdman verify beforehand. This handles
        /// an output appearing midway by rolling back ONLY files this call
        /// successfully created, never deleting pre-existing user files.
        /// </summary>
        public static bool PublishChdGroup(string stage, string outputFolder,
                                           IReadOnlyList<string> names)
        {
            if (names.Count == 0 || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
                throw new InvalidDataException("Grupo CHD vacío o con nombres repetidos.");
            foreach (var name in names)
            {
                if (name != Path.GetFileName(name) ||
                    !name.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(Path.Combine(stage, name)))
                    throw new InvalidDataException("CHD provisional inválido o ausente: " + name);
            }
            if (names.Any(name => File.Exists(Path.Combine(outputFolder, name))))
                return false;
            Directory.CreateDirectory(outputFolder);
            var committed = new List<string>();
            try
            {
                foreach (var name in names)
                {
                    var output = Path.Combine(outputFolder, name);
                    File.Move(Path.Combine(stage, name), output);
                    committed.Add(output);
                }
                return true;
            }
            catch
            {
                foreach (var file in committed)
                {
                    // This exact path was newly created by this commit.
                    // Never roll back a path we did not create.
                    if (File.Exists(file)) File.Delete(file);
                }
                throw;
            }
        }
    }
}
