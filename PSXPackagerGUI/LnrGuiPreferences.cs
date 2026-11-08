using System;
using System.IO;
using System.Text.Json;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Stores only local UI preferences. Never reads, moves or modifies CHD/PBP files.
    /// The upstream PSXPackager settings are deliberately kept separate.
    /// </summary>
    public sealed class LnrGuiPreferences
    {
        public string LastSourceFolder { get; set; } = "";
        public int MultidiscMode { get; set; }
        // Old beta.4 preferences stored the combo index (0=1, 1=5, 2=9).
        // Preserve explicitly saved legacy choices when upgrading to beta.5.
        public int CompressionIndex { get; set; } = -1;
        // New format stores the actual level; fresh installations default to 3.
        public int? CompressionLevel { get; set; }

        public int PreferredCompressionLevel =>
            CompressionLevel is 1 or 3 or 5 or 9
                ? CompressionLevel.Value
                : CompressionIndex switch
                {
                    0 => 1,
                    1 => 5,
                    2 => 9,
                    _ => 3
                };
        public bool FollowActivity { get; set; } = true;
        public double WindowWidth { get; set; } = 1020;
        public double WindowHeight { get; set; } = 830;
        // Beta.10 favours a larger list of detected CHD games.
        public double LogPanelHeight { get; set; } = 200;
        // Old versions saved the previous default height (300) without
        // marking whether the user had changed it. Migrate that exact
        // value once, preserving all other custom splitter positions.
        public int LayoutVersion { get; set; }

        private static string PreferencesPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PSXPackagerLNR", "gui-preferencias.json");

        public static LnrGuiPreferences Load()
        {
            try
            {
                if (!File.Exists(PreferencesPath))
                    return new LnrGuiPreferences { LayoutVersion = 2 };

                var preferences = JsonSerializer.Deserialize<LnrGuiPreferences>(
                    File.ReadAllText(PreferencesPath)) ?? new LnrGuiPreferences();
                if (preferences.LayoutVersion < 2)
                {
                    if (Math.Abs(preferences.LogPanelHeight - 300.0) < 1)
                        preferences.LogPanelHeight = 200;
                    preferences.LayoutVersion = 2;
                }
                return preferences;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or JsonException or ArgumentException)
            {
                // Bad preferences must never prevent the application from starting.
                return new LnrGuiPreferences();
            }
        }

        public static void Save(LnrGuiPreferences preferences)
        {
            var file = PreferencesPath;
            var folder = Path.GetDirectoryName(file);
            if (folder == null) throw new InvalidOperationException("Invalid preferences folder.");
            Directory.CreateDirectory(folder);
            var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
            // Atomic replace prevents broken JSON if Windows shuts down during save.
            var temporary = file + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, file, overwrite: true);
        }
    }
}
