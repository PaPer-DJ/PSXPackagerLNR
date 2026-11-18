using System;
using System.IO;
using System.Text.Json;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Suite-only non-destructive UI preferences. Stored separately from
    /// CHD -> PBP LNR preferences. Shutdown is deliberately never persisted.
    /// </summary>
    internal sealed class LnrSuitePreferences
    {
        public int Mode { get; set; }
        public string LastSourceFolder { get; set; } = "";
        public bool IncludeSubfolders { get; set; }
        public int ParallelConversions { get; set; } = 4;
        public bool FollowActive { get; set; } = true;
        public bool AutoScroll { get; set; } = true;
        public double WindowWidth { get; set; } = 1290;
        public double WindowHeight { get; set; } = 810;
        public double LogPanelHeight { get; set; } = 185;

        public int ValidatedParallel => ParallelConversions is 0 or 1 or 2 or 3 or 4 or 6 or 8
            ? ParallelConversions : 4;
        public int ValidatedMode => Mode >= 0 && Mode <= 7 ? Mode : 0;

        private static string PreferenceFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PSXPackagerLNR", "suite-preferencias.json");

        public static LnrSuitePreferences Load()
        {
            try
            {
                return File.Exists(PreferenceFile)
                    ? JsonSerializer.Deserialize<LnrSuitePreferences>(
                        File.ReadAllText(PreferenceFile)) ?? new LnrSuitePreferences()
                    : new LnrSuitePreferences();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                       or JsonException or ArgumentException)
            {
                return new LnrSuitePreferences();
            }
        }

        public static void Save(LnrSuitePreferences p)
        {
            var folder = Path.GetDirectoryName(PreferenceFile)
                ?? throw new InvalidOperationException("Ruta de preferencias inválida.");
            Directory.CreateDirectory(folder);
            var staging = PreferenceFile + ".tmp";
            File.WriteAllText(staging, JsonSerializer.Serialize(p,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(staging, PreferenceFile, overwrite: true);
        }
    }
}
