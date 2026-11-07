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
        public int CompressionIndex { get; set; } = 1;
        public bool FollowActivity { get; set; } = true;
        public double WindowWidth { get; set; } = 1020;
        public double WindowHeight { get; set; } = 830;
        public double LogPanelHeight { get; set; } = 300;

        private static string PreferencesPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PSXPackagerLNR", "gui-preferencias.json");

        public static LnrGuiPreferences Load()
        {
            try
            {
                if (!File.Exists(PreferencesPath)) return new LnrGuiPreferences();
                return JsonSerializer.Deserialize<LnrGuiPreferences>(File.ReadAllText(PreferencesPath))
                       ?? new LnrGuiPreferences();
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
