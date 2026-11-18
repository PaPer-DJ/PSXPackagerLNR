using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Popstation.Pbp;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Additional LNR conversion routes. Original images are never deleted.
    /// CHDMAN is an optional separately supplied MAME tool. The direct
    /// CHD-to-PBP workflow remains independent and does not require it.
    /// </summary>
    public partial class LnrDiscSuiteWindow : Window
    {
        private readonly ObservableCollection<DiscJob> _jobs = new();
        private readonly List<string> _messages = new();
        private readonly HashSet<int> _manuallySizedColumns = new();
        private bool _settingColumnWidths;
        private bool _cancelRequested;
        private bool _includeSubfolders;
        private readonly LnrSuitePreferences _preferences;
        private readonly HashSet<Process> _activeProcesses = new();
        private readonly Dictionary<DiscJob, Stopwatch> _activeClocks = new();
        private readonly DispatcherTimer _progressTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        private Stopwatch _batchClock;
        private bool _pauseRequested;
        private bool _pauseComplete;
        private TaskCompletionSource<bool> _resumeGate;
        private int _inFlightJobs;
        private int _workers = 1;
        private int _success, _skipped, _errors;
        private long _calibratedBytes;
        private double _calibratedSeconds;
        private bool _completedNormally;
        private bool _batchCanceled;
        // Countdown is local to this window; Windows gets no scheduled command.
        private readonly DispatcherTimer _shutdownCountdown = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        private bool _shutdownPending;
        private int _shutdownSecondsRemaining;
        private CancellationTokenSource _cancel;
        private bool _running;
        private readonly CultureInfo _es = CultureInfo.GetCultureInfo("es-ES");

        public LnrDiscSuiteWindow()
        {
            InitializeComponent();
            Title = "PSXPackagerLNR · " + LnrVersionInfo.Display + " | Suite de discos";
            JobsList.ItemsSource = _jobs;
            TrackManualColumnResizing();
            _shutdownCountdown.Tick += ShutdownCountdown_OnTick;
            _progressTimer.Tick += (sender, e) => UpdateSuiteProgress();
            ShutdownCheck.IsChecked = false; // Never restored from preferences.
            _preferences = LnrSuitePreferences.Load();
            ModeBox.SelectedIndex = _preferences.ValidatedMode;
            var mainPrefs = LnrGuiPreferences.Load();
            SourceBox.Text = Directory.Exists(_preferences.LastSourceFolder)
                ? _preferences.LastSourceFolder
                : Directory.Exists(mainPrefs.LastSourceFolder)
                    ? mainPrefs.LastSourceFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _includeSubfolders = _preferences.IncludeSubfolders;
            FollowActiveCheck.IsChecked = _preferences.FollowActive;
            AutoScrollCheck.IsChecked = _preferences.AutoScroll;
            foreach (ComboBoxItem item in ParallelCombo.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out var selected) &&
                    selected == _preferences.ValidatedParallel)
                {
                    ParallelCombo.SelectedItem = item;
                    break;
                }
            }
            if (_preferences.WindowWidth >= MinWidth &&
                _preferences.WindowWidth <= SystemParameters.WorkArea.Width)
                Width = _preferences.WindowWidth;
            if (_preferences.WindowHeight >= MinHeight &&
                _preferences.WindowHeight <= SystemParameters.WorkArea.Height)
                Height = _preferences.WindowHeight;
            if (_preferences.LogPanelHeight >= 100 && _preferences.LogPanelHeight <= 450)
                SuiteLogPanelRow.Height = new GridLength(_preferences.LogPanelHeight);
            var localChdman = Path.Combine(AppContext.BaseDirectory, "chdman.exe");
            var importedChdman = ImportedChdmanPath;
            ChdmanBox.Text = File.Exists(localChdman) ? localChdman
                           : File.Exists(importedChdman) ? importedChdman : "";
            UpdateChdmanUi();
            Log("PBP → BIN/CUE y PBP → ISO de datos no necesitan CHDMAN.");
            Log("La creación/extracción de CHD utiliza CHDMAN de MAME. No cambia ni elimina originales.");
            Log("Los PBP multidisco se extraerán y convertirán a un CHD independiente por disco.");
            Scan();
        }

        private int Mode => ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
        private int ParallelSetting => int.TryParse(
            (ParallelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var count)
                ? count : 4;
        private int ParallelCount => ParallelSetting == 0
            ? Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
            : ParallelSetting;

        private static readonly string[] ExcludedSuiteFolders =
            { "PBP", "CHD", "BIN_CUE", "ISO", "GDI", "CHD_RECOMPRIMIDO", "LOGS", "_TEMP", ".tools" };

        private void SavePreferences()
        {
            try
            {
                LnrSuitePreferences.Save(new LnrSuitePreferences
                {
                    Mode = Mode,
                    LastSourceFolder = SourceBox.Text.Trim().Trim('"'),
                    IncludeSubfolders = _includeSubfolders,
                    ParallelConversions = ParallelSetting,
                    FollowActive = FollowActiveCheck.IsChecked == true,
                    AutoScroll = AutoScrollCheck.IsChecked == true,
                    WindowWidth = ActualWidth,
                    WindowHeight = ActualHeight,
                    LogPanelHeight = SuiteLogPanelRow.ActualHeight
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException)
            {
                Log("AVISO: no se pudieron guardar las preferencias de la Suite: " + ex.Message);
            }
        }
        private string SourceExtension => Mode <= 2 ? ".pbp" :
                                              Mode == 5 ? ".gdi" : ".chd";
        private bool NeedsChdman => Mode == 0 || Mode >= 3;
        private bool IsDreamcastVerify => Mode == 7;
        private string OutputSubdirectory => Mode switch
        {
            0 => "CHD",
            1 => "BIN_CUE",
            2 => "ISO",
            3 => "BIN_CUE",
            4 => "ISO",
            5 => "CHD",
            6 => "GDI",
            7 => "",
            _ => throw new InvalidOperationException("Modo de Suite no válido.")
        };

        private void Log(string message)
        {
            foreach (var line in (message ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line.TrimEnd();
                _messages.Add(entry);
                // Add a separator before the next entry, not a blank line after the last one.
                LogBox.AppendText((LogBox.Text.Length == 0 ? "" : Environment.NewLine) + entry);
            }
            if (LogBox.Text.Length > 120000)
                LogBox.Text = LogBox.Text.Substring(LogBox.Text.Length - 80000);
            if (AutoScrollCheck.IsChecked == true)
                LogBox.ScrollToEnd();
        }

        private void ClearLog_OnClick(object sender, RoutedEventArgs e)
        {
            // Clear only the visible/session log; leave all source and output files intact.
            _messages.Clear();
            LogBox.Clear();
        }

        private void CopyLog_OnClick(object sender, RoutedEventArgs e)
        {
            if (_messages.Count == 0) return;
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, _messages));
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                MessageBox.Show(this, "No se pudo copiar el registro: " + ex.Message,
                                "Portapapeles ocupado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenOutput_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var source = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
                var output = Path.Combine(source, OutputSubdirectory);
                if (!Directory.Exists(output))
                {
                    MessageBox.Show(this,
                        "La carpeta de salida todavía no existe:\n\n" + output +
                        "\n\nSe creará al procesar los archivos.",
                        "Carpeta de salida", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Process.Start(new ProcessStartInfo(output) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or InvalidOperationException or ArgumentException
                                       or Win32Exception)
            {
                MessageBox.Show(this, "No se pudo abrir la carpeta: " + ex.Message,
                                "Carpeta de salida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveLog()
        {
            if (_messages.Count == 0) return;
            try
            {
                var folder = Path.Combine(AppContext.BaseDirectory, "LOGS");
                try { Directory.CreateDirectory(folder); }
                catch
                {
                    folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PSXPackagerLNR", "Logs");
                    Directory.CreateDirectory(folder);
                }
                var file = Path.Combine(folder, "LNR_SUITE_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllLines(file, _messages, new UTF8Encoding(true));
                Log("Informe guardado: " + file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException)
            {
                Log("AVISO: no se pudo guardar el registro: " + ex.Message);
            }
        }

        private void JobsList_OnLoaded(object sender, RoutedEventArgs e) => ResizeSuiteColumns();

        private void JobsList_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) ResizeSuiteColumns();
        }

        private void TrackManualColumnResizing()
        {
            if (JobsList.View is not GridView view) return;
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                GridViewColumn.WidthProperty, typeof(GridViewColumn));
            if (descriptor == null) return;
            for (var i = 0; i < view.Columns.Count; i++)
            {
                var index = i;
                descriptor.AddValueChanged(view.Columns[i], (_, _) =>
                {
                    if (IsLoaded && !_settingColumnWidths)
                        _manuallySizedColumns.Add(index);
                });
            }
        }

        /// <summary>
        /// First column uses the longest visible filename and any free width.
        /// Other columns are centred and measured by values/headings, and manual
        /// widths are preserved on Analyze, window resize and job status changes.
        /// </summary>
        private void ResizeSuiteColumns()
        {
            if (JobsList == null || JobsList.ActualWidth < 550 ||
                JobsList.View is not GridView view || view.Columns.Count != 7 ||
                _settingColumnWidths)
                return;

            double TextWidth(string value, bool header = false)
            {
                var probe = new TextBlock
                {
                    Text = value ?? "—",
                    FontFamily = JobsList.FontFamily,
                    FontSize = JobsList.FontSize,
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal
                };
                probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return probe.DesiredSize.Width;
            }
            double Measured(string header, Func<DiscJob, string> content,
                            double minimum, double maximum = 450)
            {
                var longest = TextWidth(header, true);
                foreach (var job in _jobs)
                    longest = Math.Max(longest, TextWidth(content(job)));
                return Math.Clamp(Math.Ceiling(longest + 24), minimum, maximum);
            }
            var ideal = new double[7];
            ideal[1] = Measured("Origen", j => j.SizeText, 86, 125);
            ideal[2] = Measured("Salida", j => j.OutputSizeText, 86, 125);
            ideal[3] = Measured("Variación", j => j.VariationText, 100, 138);
            ideal[4] = Measured("Tiempo", j => j.ElapsedText, 70, 100);
            ideal[5] = Measured("Archivos", j => j.OutputCount, 76, 100);
            ideal[6] = Measured("Estado / Progreso", j => j.DisplayStatus, 196, 270);

            for (var index = 1; index < ideal.Length; index++)
                if (_manuallySizedColumns.Contains(index))
                    ideal[index] = view.Columns[index].Width;

            var available = JobsList.ActualWidth - SystemParameters.VerticalScrollBarWidth - 8;
            var longestName = Measured("Archivo origen", j => j.DisplayName, 170, 1900);
            ideal[0] = _manuallySizedColumns.Contains(0)
                ? view.Columns[0].Width
                : Math.Max(longestName, Math.Floor(available - ideal.Skip(1).Sum()));
            _settingColumnWidths = true;
            try
            {
                for (var i = 0; i < ideal.Length; i++)
                {
                    if (_manuallySizedColumns.Contains(i)) continue;
                    if (Math.Abs(view.Columns[i].Width - ideal[i]) > 0.5)
                        view.Columns[i].Width = ideal[i];
                }
            }
            finally { _settingColumnWidths = false; }
        }

        private void Mode_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !_running)
            {
                UpdateChdmanUi();
                Scan();
                SavePreferences();
            }
        }

        private void FollowActive_OnChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || !_running || FollowActiveCheck.IsChecked != true)
                return;
            var currentlyActive = _jobs.FirstOrDefault(job => job.Status == "Convirtiendo");
            if (currentlyActive != null) FollowActive(currentlyActive);
        }

        private void FollowActive(DiscJob job)
        {
            if (FollowActiveCheck.IsChecked != true) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_running && FollowActiveCheck.IsChecked == true &&
                    _jobs.Contains(job) && job.Status == "Convirtiendo")
                    JobsList.ScrollIntoView(job);
            }));
        }

        private void UpdateChdmanUi()
        {
            var required = NeedsChdman;
            ChdmanLabel.Text = required ? "CHDMAN *" : "CHDMAN (no necesario)";
            ChdmanBox.IsEnabled = required && !_running;
            FindChdmanButton.IsEnabled = required && !_running;
            ImportChdmanButton.IsEnabled = required && !_running;
        }

        private void Source_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _running) return;
            Scan();
            SavePreferences();
            e.Handled = true;
        }

        private void Source_OnLostKeyboardFocus(object sender,
                                                KeyboardFocusChangedEventArgs e)
        {
            if (IsLoaded && !_running)
            {
                Scan();
                SavePreferences();
            }
        }

        private void Browse_OnClick(object sender, RoutedEventArgs e)
        {
            var picker = new LnrFolderPickerWindow(
                SourceBox.Text, _includeSubfolders, SourceExtension, ExcludedSuiteFolders)
            {
                Owner = this
            };
            if (picker.ShowDialog() != true) return;
            SourceBox.Text = picker.SelectedFolder;
            // The folder picker is the single place to set recursive scanning.
            // Keep the value in Suite-only preferences for subsequent runs.
            _includeSubfolders = picker.SelectedIncludeSubfolders;
            Scan();
            SavePreferences();
        }

        private static string ImportedChdmanPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PSXPackagerLNR", "Tools", "chdman.exe");

        private void ImportChdman_OnClick(object sender, RoutedEventArgs e)
        {
            // A standard MessageBox cannot render hyperlinks. This modal
            // introduction lets users open the Recalbox, OnionUI or MAME
            // sources in their browser BEFORE answering Yes or No.
            // Opening a source is informational: import still requires
            // an explicit Yes and selection of a local ZIP.
            var intro = new LnrChdmanImportDialog { Owner = this };
            if (intro.ShowDialog() != true) return;

            var picker = new OpenFileDialog
            {
                Title = "Selecciona el paquete ZIP que contiene chdman.exe",
                Filter = "Archivos ZIP (*.zip)|*.zip",
                CheckFileExists = true,
                Multiselect = false
            };
            if (picker.ShowDialog(this) != true) return;

            var target = ImportedChdmanPath;
            var temporary = target + ".tmp";
            try
            {
                using var zipFile = File.OpenRead(picker.FileName);
                using var zip = new ZipArchive(zipFile, ZipArchiveMode.Read, leaveOpen: false);
                var candidates = zip.Entries
                    .Where(entry => string.Equals(Path.GetFileName(entry.FullName),
                        "chdman.exe", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.Contains("..", StringComparison.Ordinal) &&
                        entry.Length >= 100000 && entry.Length <= 50L * 1024 * 1024)
                    .ToList();

                if (candidates.Count != 1)
                    throw new InvalidDataException(
                        "El ZIP debe contener exactamente un chdman.exe de Windows " +
                        "(y no varios ejecutables con el mismo nombre).");

                var entry = candidates[0];
                var folder = Path.GetDirectoryName(target)!;
                Directory.CreateDirectory(folder);
                if (File.Exists(target))
                {
                    if (MessageBox.Show(this,
                        "Ya hay un CHDMAN importado. ¿Quieres sustituirlo por el del nuevo ZIP?",
                        "Sustituir CHDMAN", MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                using (var input = entry.Open())
                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                                                    FileShare.None))
                {
                    input.CopyTo(output);
                }

                using (var check = File.OpenRead(temporary))
                {
                    var signature = new byte[2];
                    if (check.Length != entry.Length ||
                        check.Read(signature, 0, 2) != 2 ||
                        signature[0] != 0x4D || signature[1] != 0x5A)
                        throw new InvalidDataException(
                            "El archivo extraído no parece un ejecutable Windows válido.");
                }

                var checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporary)));
                File.Move(temporary, target, overwrite: true);
                ChdmanBox.Text = target;
                Log("CHDMAN importado desde ZIP: " + Path.GetFileName(picker.FileName));
                Log("SHA-256 del ejecutable importado: " + checksum);
                Log("Se usará en la suite; no se ha ejecutado durante la importación.");
                MessageBox.Show(this,
                    "CHDMAN ya está preparado en tu carpeta de usuario.\n\n" +
                    "No tendrás que copiarlo junto a la aplicación.\n" +
                    "El archivo procede del ZIP que has seleccionado; " +
                    "la comprobación de formato y SHA-256 no certifica su autoría.",
                    "Importación completada", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or InvalidDataException or ArgumentException
                                       or NotSupportedException)
            {
                Log("ERROR al importar CHDMAN: " + ex.Message);
                MessageBox.Show(this, "No se pudo importar CHDMAN:\n" + ex.Message,
                    "Paquete no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private void FindChdman_OnClick(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog
            {
                Title = "Selecciona chdman.exe de MAME",
                Filter = "Herramienta CHDMAN (chdman.exe)|chdman.exe",
                CheckFileExists = true
            };
            if (picker.ShowDialog(this) == true)
                ChdmanBox.Text = picker.FileName;
        }

        private void Analyze_OnClick(object sender, RoutedEventArgs e) => Scan();

        private void Help_OnClick(object sender, RoutedEventArgs e)
        {
            new LnrSuiteHelpWindow { Owner = this }.ShowDialog();
        }

        private IReadOnlyList<string> OutputTargets(DiscJob job, string root)
        {
            var dest = Path.Combine(root, OutputSubdirectory, job.RelativeFolder);
            var baseName = Path.GetFileNameWithoutExtension(job.Source);
            return Mode switch
            {
                0 => LnrSuiteDiscNaming.ChdNames(job.Source, job.DiscCount)
                         .Select(name => Path.Combine(dest, name)).ToArray(),
                1 => new[] { Path.Combine(dest, baseName) },
                2 => new[] { Path.Combine(dest, baseName + ".iso") },
                3 => new[] { Path.Combine(dest, baseName) },
                4 => new[] { Path.Combine(dest, baseName + ".iso") },
                5 => new[] { Path.Combine(dest, baseName + ".chd") },
                6 => new[] { Path.Combine(dest, baseName) },
                7 => Array.Empty<string>(),
                _ => throw new InvalidOperationException("Modo de Suite no válido.")
            };
        }

        private static void VerifyPbpMode1(PbpDiscEntry disc)
        {
            // The PBP's internal TOC distinguishes DATA from AUDIO but does
            // not distinguish MODE1 from MODE2. Check both the track layout
            // and representative decompressed sectors before planning ISO.
            var singleDataTrack = disc.TOC != null && disc.TOC.Count == 1 &&
                disc.TOC[0].TrackNo == 1 &&
                disc.TOC[0].TrackType == PSXPackager.Common.TrackTypeEnum.Data;
            using var rawDisc = disc.GetDiscStream();
            LnrDataTrackIsoExporter.CheckPbpMode1Preflight(rawDisc, singleDataTrack);
        }

        private void Scan()
        {
            if (_running) return;
            if (_shutdownPending)
                CancelShutdownCountdown("Apagado automático cancelado al iniciar otro análisis.");

            _jobs.Clear();
            _success = _skipped = _errors = 0;
            _calibratedBytes = 0;
            _calibratedSeconds = 0;
            _batchClock = null;
            _completedNormally = false;
            _batchCanceled = false;
            BatchProgress.Value = 0;
            ProgressLabel.Text = "0/0 · sin iniciar";
            EtaLabel.Text = "Tiempo restante: sin calcular";
            try
            {
                var root = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
                if (!Directory.Exists(root))
                {
                    CountLabel.Text = "Carpeta no encontrada";
                    ConvertButton.IsEnabled = false;
                    return;
                }

                foreach (var subfolder in LnrChdFolderScanner.EnumerateFolders(
                             root, _includeSubfolders, Log, ExcludedSuiteFolders))
                {
                    var relativeFolder = LnrChdFolderScanner.RelativeFolder(root, subfolder);
                    foreach (var path in LnrChdFolderScanner.FindFiles(subfolder, SourceExtension, Log))
                    {
                        try
                        {
                            var size = new FileInfo(path).Length;
                            var job = new DiscJob(path, relativeFolder, size, Mode);
                            if (Mode == 5)
                            {
                                try
                                {
                                    // Sum the descriptor and the actual GD-ROM
                                    // track files. The tiny GDI text length
                                    // alone is not a meaningful input size.
                                    var gdi = LnrDreamcastGdi.Validate(path);
                                    job = new DiscJob(path, relativeFolder, gdi.InputBytes, Mode);
                                }
                                catch (Exception ex)
                                {
                                    job.PlanError = "GDI Dreamcast incompatible: " + ex.Message;
                                }
                            }
                            if (Mode is 6 or 7)
                            {
                                try { VerifyChdHeader(path); }
                                catch (Exception ex)
                                {
                                    job.PlanError = "CHD no válido: " + ex.Message;
                                }
                            }
                            if (Mode <= 2)
                            {
                                try
                                {
                                    // Read the PBP's REAL internal disc table, not
                                    // alphabetic .cue filenames, to reserve outputs.
                                    using var input = File.OpenRead(path);
                                    var pbp = new PbpReader(input);
                                    var discs = pbp.Discs.Count;
                                    if (discs is < 1 or > 5)
                                        throw new InvalidDataException("Cantidad de discos PBP inválida.");
                                    job.DiscCount = discs;
                                    if (Mode == 2)
                                    {
                                        if (discs != 1)
                                            throw new InvalidDataException(
                                                "El PBP tiene " + discs + " discos. " +
                                                "ISO solo admite un disco MODE1; usa PBP → BIN/CUE.");
                                        VerifyPbpMode1(pbp.Discs[0]);
                                    }
                                }
                                catch (InvalidDataException ex) when (Mode == 2)
                                {
                                    // Mark unsupported discs BEFORE the user
                                    // starts the batch. These jobs never invoke
                                    // the converter; the rest still proceed.
                                    job.PlanError = "ISO no compatible: " + ex.Message;
                                }
                                catch (Exception ex)
                                {
                                    job.PlanError = "No se pudo analizar el PBP: " + ex.Message;
                                }
                            }
                            if (job.PlanError == null)
                                job.PlannedOutputs = OutputTargets(job, root).ToArray();
                            _jobs.Add(job);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                                   or ArgumentException or NotSupportedException)
                        {
                            Log("AVISO: archivo inaccesible " + Path.GetFileName(path) + ": " + ex.Message);
                        }
                    }
                }

                // Compare ACTUAL future target paths before starting workers.
                // This prevents two PBP sources in the same folder from
                // generating the same "Game (Disc N).chd" in parallel.
                var reserved = new Dictionary<string, DiscJob>(StringComparer.OrdinalIgnoreCase);
                foreach (var job in _jobs)
                {
                    if (job.PlanError != null) continue;
                    foreach (var target in job.PlannedOutputs)
                    {
                        if (reserved.TryGetValue(target, out var existing) &&
                            !ReferenceEquals(existing, job))
                        {
                            job.PlanError = "Conflicto de nombre de salida: " + target;
                            existing.PlanError = "Conflicto de nombre de salida: " + target;
                        }
                        else reserved[target] = job;
                    }
                }
                var already = 0;
                var incompatible = 0;
                foreach (var job in _jobs)
                {
                    if (job.PlanError != null)
                    {
                        job.Status = "ERROR";
                        incompatible++;
                        Log("NO SE PROCESARÁ: " + job.DisplayName + ": " + job.PlanError);
                        continue;
                    }
                    if (job.PlannedOutputs.Any(target =>
                            File.Exists(target) || Directory.Exists(target)))
                    {
                        job.Status = "Saltado / Ya existe";
                        already++;
                    }
                }

                CountLabel.Text = _jobs.Count + " archivo(s)" +
                    (_includeSubfolders ? " (incluidas subcarpetas)" : "") +
                    (already > 0 ? " · " + already + " ya existen" : "") +
                    (incompatible > 0 ? " · " + incompatible + " no compatibles" : "");
                ConvertButton.IsEnabled = _jobs.Count > 0;
                OutputLabel.Text = IsDreamcastVerify
                    ? "Solo análisis y verificación CHD de Dreamcast · sin crear archivos."
                    : "Salida: " + Path.Combine(root, OutputSubdirectory) +
                      (_includeSubfolders ? " (estructura de subcarpetas conservada)" : "") +
                      " · originales intactos.";
                Log($"Analizados {_jobs.Count} archivos {SourceExtension} para {ModeBox.Text}; " +
                    $"subcarpetas {(_includeSubfolders ? "sí" : "no")}; " +
                    $"procesos: {ParallelCount}; ya existentes: {already}.");
                UpdateSuiteProgress();
                ResizeSuiteColumns();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
            {
                ConvertButton.IsEnabled = false;
                CountLabel.Text = "Error de lectura";
                Log("ERROR al analizar: " + ex.Message);
            }
        }

        private void SetRunning(bool running)
        {
            _running = running;
            BrowseButton.IsEnabled = !running;
            FindChdmanButton.IsEnabled = !running && NeedsChdman;
            ImportChdmanButton.IsEnabled = !running && NeedsChdman;
            AnalyzeButton.IsEnabled = !running;
            ModeBox.IsEnabled = !running;
            ParallelCombo.IsEnabled = !running;
            SourceBox.IsEnabled = !running;
            ChdmanBox.IsEnabled = !running && NeedsChdman;
            CancelButton.IsEnabled = running && !_cancelRequested;
            ShutdownCheck.IsEnabled = !running && !_shutdownPending;
            RefreshConvertButton();
        }

        private void RefreshConvertButton()
        {
            if (!_running)
            {
                ConvertButton.Content = "Convertir lote";
                ConvertButton.ContentTemplate =
                    (DataTemplate)FindResource("LnrConvertButtonTemplate");
                ConvertButton.IsEnabled = _jobs.Count > 0;
                return;
            }
            ConvertButton.Content = !_pauseRequested ? "Pausar" :
                                    _pauseComplete ? "Reanudar" : "Pausando...";
            ConvertButton.ContentTemplate =
                (DataTemplate)FindResource(!_pauseRequested || _pauseComplete
                    ? "LnrConvertButtonTemplate"
                    : _pauseRequested && !_pauseComplete
                        ? "LnrPausingButtonTemplate" : "LnrPauseButtonTemplate");
            if (_running && !_pauseRequested)
                ConvertButton.ContentTemplate = (DataTemplate)FindResource("LnrPauseButtonTemplate");
            ConvertButton.IsEnabled = !_cancelRequested &&
                                      (!_pauseRequested || _pauseComplete);
        }

        private void TogglePause()
        {
            if (!_running || _cancelRequested) return;
            if (!_pauseRequested)
            {
                _pauseRequested = true;
                _pauseComplete = false;
                _resumeGate = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Log("PAUSANDO: no se asignarán nuevos archivos. Esperando a que terminen " +
                    _inFlightJobs + " trabajo(s) activo(s) con todas sus operaciones.");
                MarkPausedIfSafe();
            }
            else if (_pauseComplete)
            {
                _pauseRequested = false;
                _pauseComplete = false;
                _batchClock?.Start();
                _resumeGate?.TrySetResult(true);
                _resumeGate = null;
                Log("REANUDADO: continúan los archivos pendientes.");
            }
            RefreshConvertButton();
            UpdateSuiteProgress();
        }

        private void MarkPausedIfSafe()
        {
            if (!_pauseRequested || _pauseComplete || _inFlightJobs != 0)
                return;
            _pauseComplete = true;
            _batchClock?.Stop();
            Log("EN PAUSA: no queda ningún archivo, disco ni CHDMAN activo.");
            RefreshConvertButton();
            UpdateSuiteProgress();
        }

        private async Task WaitIfPausedAsync(CancellationToken token)
        {
            while (_pauseRequested)
            {
                token.ThrowIfCancellationRequested();
                var gate = _resumeGate ??
                    throw new InvalidOperationException("Falta la señal de reanudación.");
                await gate.Task.WaitAsync(token);
            }
        }

        private async void Convert_OnClick(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                TogglePause();
                return;
            }
            if (_shutdownPending)
            {
                MessageBox.Show(this,
                    "Hay un apagado pendiente. Cancélalo antes de iniciar otra conversión.",
                    "Apagado programado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Scan();
            if (_jobs.Count == 0) return;

            var chdman = ChdmanBox.Text.Trim().Trim('"');
            if (NeedsChdman && (!File.Exists(chdman) ||
                !string.Equals(Path.GetFileName(chdman), "chdman.exe",
                    StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this,
                    "Este modo requiere chdman.exe de MAME. Usa «Importar ZIP...» " +
                    "o «Localizar...».\n\nCHD → PBP directo sigue funcionando sin CHDMAN.",
                    "Falta CHDMAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var root = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
            var dest = Path.Combine(root, OutputSubdirectory);
            var confirmation = $"Modo: {ModeBox.Text}\n" +
                $"Archivos de origen: {_jobs.Count}\n" +
                $"Conversiones simultáneas: {ParallelCount}\n" +
                $"También subcarpetas: {(_includeSubfolders ? "Sí (se conserva la estructura)" : "No")}\n" +
                $"Destino: {dest}\n\n" +
                "Los originales se conservarán siempre.\n" +
                "No se sobrescribirán archivos ni carpetas existentes.\n" +
                "La pausa espera a que se complete íntegramente el trabajo activo.\n\n" +
                (Mode == 0
                    ? "PBP → CHD utiliza BIN/CUE temporales y CHDMAN; cada PBP multidisco " +
                      "se publica como un grupo indivisible de CHD (Disc 1), (Disc 2), etc."
                    : Mode == 1
                    ? "PBP → BIN/CUE renombra todos los discos y actualiza sus referencias CUE."
                    : Mode == 2
                    ? "MODO [ NO RECOMENDADO ]. SOLO un disco y una pista CD de datos MODE1. " +
                      "Los PBP MODE2, audio, multidisco o no compatibles se marcan como ERROR " +
                      "en el análisis y no se convierten; el resto del lote continúa. " +
                      "Se verifica cada sector al exportar. Para preservar un juego PS1 utiliza BIN/CUE."
                    : "Comprueba que has escogido el tipo de soporte correcto (CD o DVD).") +
                "\n\n¿Iniciar el lote?";
            if (MessageBox.Show(this, confirmation, "Confirmar lote de la suite",
                                MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
                return;

            var shutdownRequested = ShutdownCheck.IsChecked == true;
            if (shutdownRequested)
            {
                var response = MessageBox.Show(this,
                    "APAGAR EL EQUIPO DESPUÉS DEL LOTE\n\n" +
                    "Solo si se convierten archivos nuevos y el lote termina sin errores " +
                    "ni cancelaciones. No se apaga mientras está en pausa.\n\n" +
                    "Al acabar tendrás 60 segundos para cancelar desde esta ventana. " +
                    "Windows intentará un apagado normal SIN forzar otras aplicaciones.\n\n" +
                    "¿Confirmas el apagado automático opcional?",
                    "Confirmar apagado automático",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (response != MessageBoxResult.Yes)
                {
                    ShutdownCheck.IsChecked = false;
                    return;
                }
            }

            _cancelRequested = false;
            _pauseRequested = _pauseComplete = false;
            _resumeGate = null;
            _inFlightJobs = 0;
            _cancel = new CancellationTokenSource();
            _activeProcesses.Clear();
            _activeClocks.Clear();
            _success = _skipped = _errors = 0;
            _calibratedBytes = 0;
            _calibratedSeconds = 0;
            _completedNormally = false;
            _batchCanceled = false;
            _workers = Math.Min(ParallelCount, _jobs.Count);
            _batchClock = Stopwatch.StartNew();
            SetRunning(true);
            _progressTimer.Start();
            Log("Procesamiento concurrente de la Suite: " + _workers + " trabajo(s) simultáneos.");
            try
            {
                Directory.CreateDirectory(dest);
                await ProcessAllAsync(root, chdman, _cancel.Token);
                _completedNormally = !_cancel.IsCancellationRequested;
                Log($"FIN. Correctos: {_success}; saltados: {_skipped}; errores: {_errors}.");
            }
            catch (OperationCanceledException)
            {
                _batchCanceled = true;
                Log("Lote cancelado. Los originales se conservan y los resultados " +
                    "que ya terminaron correctamente permanecen disponibles.");
            }
            catch (Exception ex)
            {
                _errors++;
                Log("ERROR GENERAL: " + ex.Message);
            }
            finally
            {
                _progressTimer.Stop();
                _batchClock?.Stop();
                _activeClocks.Clear();
                _pauseRequested = _pauseComplete = false;
                _resumeGate?.TrySetResult(true);
                _resumeGate = null;
                _inFlightJobs = 0;
                SetRunning(false);
                _activeProcesses.Clear();
                _cancel?.Dispose();
                _cancel = null;
                UpdateSuiteProgress();
                ShutdownCheck.IsChecked = false; // Never persist system controls.
                SavePreferences();
                SaveLog();
                if (shutdownRequested)
                {
                    if (_completedNormally && !_cancelRequested && !_batchCanceled &&
                        _errors == 0 && _success > 0)
                        BeginShutdownCountdown();
                    else
                    {
                        Log("APAGADO OMITIDO: lote cancelado, con errores o sin conversiones nuevas.");
                        SaveLog();
                    }
                }
            }
        }

        private async Task ProcessAllAsync(string root, string chdman, CancellationToken token)
        {
            var nextIndex = -1;
            var total = _jobs.Count;
            async Task WorkerAsync()
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (nextIndex >= total - 1) return;
                    await WaitIfPausedAsync(token);
                    token.ThrowIfCancellationRequested();
                    var index = Interlocked.Increment(ref nextIndex);
                    if (index >= total) return;
                    _inFlightJobs++;
                    var job = _jobs[index];
                    var timer = Stopwatch.StartNew();
                    _activeClocks[job] = timer;
                    try
                    {
                        await ProcessOneAsync(job, root, chdman, token);
                    }
                    finally
                    {
                        timer.Stop();
                        job.Elapsed = timer.Elapsed;
                        _activeClocks.Remove(job);
                        _inFlightJobs--;
                        MarkPausedIfSafe();
                        UpdateSuiteProgress();
                        ResizeSuiteColumns();
                    }
                }
            }

            var workers = Enumerable.Range(0, _workers).Select(_ => WorkerAsync()).ToArray();
            await Task.WhenAll(workers);
        }

        private async Task ProcessOneAsync(DiscJob job, string root,
                                            string chdman, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (job.PlanError != null)
                    throw new InvalidDataException(job.PlanError);

                if (job.PlannedOutputs.Any(path => File.Exists(path) || Directory.Exists(path)))
                {
                    _skipped++;
                    job.Status = "Saltado / Ya existe";
                    job.OutputBytes = MeasureOutputBytes(job);
                    Log("SALTADO: " + job.DisplayName + " ya tiene uno o más destinos.");
                    return;
                }

                job.Status = "Convirtiendo";
                job.CurrentPhase = "Iniciando";
                job.HasProgress = false;
                FollowActive(job);
                var dest = Path.Combine(root, OutputSubdirectory, job.RelativeFolder);
                Directory.CreateDirectory(dest);

                int made = Mode switch
                {
                    0 => await PbpToChdAsync(job, dest, chdman, token),
                    1 => await PbpToCueAsync(job, dest, token),
                    2 => await PbpToIsoAsync(job, dest, token),
                    3 => await ChdToCueAsync(job, dest, chdman, token),
                    _ => await ChdToIsoAsync(job, dest, chdman, token)
                };

                if (made == 0)
                {
                    _skipped++;
                    job.Status = "Saltado / Ya existe";
                    job.OutputBytes = MeasureOutputBytes(job);
                }
                else
                {
                    _success++;
                    job.OutputCount = made.ToString(CultureInfo.InvariantCulture);
                    job.OutputBytes = MeasureOutputBytes(job);
                    job.ProgressPercent = 100;
                    job.Status = "Completado";
                    _calibratedBytes += job.SourceBytes;
                    _calibratedSeconds += Math.Max(0.1,
                        _activeClocks.TryGetValue(job, out var clock)
                            ? clock.Elapsed.TotalSeconds : 0.1);
                    Log("OK: " + job.DisplayName + " | Origen " + FormatSize(job.SourceBytes) +
                        " → Salida " + job.OutputSizeText + " | variación " +
                        job.VariationText + ".");
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = "Cancelado";
                throw;
            }
            catch (Exception ex)
            {
                _errors++;
                job.Status = "ERROR";
                Log("ERROR " + job.DisplayName + ": " + ex.Message);
            }
        }

        private static string FormatSize(long size) =>
            (size / 1048576.0).ToString("N2", CultureInfo.GetCultureInfo("es-ES")) + " MB";

        private static long MeasureOutputBytes(DiscJob job)
        {
            long total = 0;
            foreach (var target in job.PlannedOutputs)
            {
                if (File.Exists(target))
                    total += new FileInfo(target).Length;
                else if (Directory.Exists(target))
                {
                    // Each target directory is specific to this source job.
                    foreach (var path in Directory.EnumerateFiles(
                                 target, "*", SearchOption.AllDirectories))
                        total += new FileInfo(path).Length;
                }
            }
            return total;
        }

        private static string ReadableTime(TimeSpan value) =>
            value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") :
            value.TotalSeconds < 1 ? "menos de 1 s" : value.ToString(@"mm\:ss");

        private void UpdateSuiteProgress()
        {
            if (BatchProgress == null || ProgressLabel == null) return;
            var completed = _success + _skipped + _errors;
            var total = _jobs.Count;
            BatchProgress.Value = total == 0 ? 0 : 100.0 * completed / total;
            ProgressLabel.Text = $"{completed}/{total} · Correctos: {_success} · " +
                                 $"Saltados: {_skipped} · Errores: {_errors}" +
                                 (_pauseRequested ? (_pauseComplete ? " · EN PAUSA" : " · PAUSANDO") : "");
            if (!_running)
            {
                EtaLabel.Text = _batchCanceled ? "Lote cancelado" :
                    _completedNormally && total > 0
                        ? (_errors == 0 ? "Lote finalizado" : "Lote finalizado con errores")
                        : "Tiempo restante: sin calcular";
                if (_batchClock != null)
                    EtaLabel.Text += " · Tiempo del lote: " + ReadableTime(_batchClock.Elapsed);
                return;
            }
            if (_pauseRequested)
            {
                EtaLabel.Text = _pauseComplete
                    ? "EN PAUSA · Sin juegos activos · Pulsa Reanudar"
                    : "PAUSANDO · Esperando " + _inFlightJobs + " archivo(s) activos";
                return;
            }
            if (_calibratedBytes <= 0 || _calibratedSeconds <= 0)
            {
                EtaLabel.Text = "Tiempo restante: calculando tras el primer archivo…";
                return;
            }
            var secondsPerByte = _calibratedSeconds / _calibratedBytes;
            double remaining = 0;
            foreach (var job in _jobs)
            {
                if (job.Status != "Pendiente" && job.Status != "Convirtiendo") continue;
                var estimate = job.SourceBytes * secondsPerByte;
                if (job.Status == "Convirtiendo" &&
                    _activeClocks.TryGetValue(job, out var clock))
                {
                    estimate -= clock.Elapsed.TotalSeconds;
                    if (estimate <= 0)
                    {
                        EtaLabel.Text = "Tiempo restante: recalculando…";
                        return;
                    }
                }
                remaining += estimate;
            }
            remaining /= Math.Max(1, _workers);
            EtaLabel.Text = double.IsFinite(remaining)
                ? "Tiempo restante estimado: ~" +
                  ReadableTime(TimeSpan.FromSeconds(Math.Min(remaining, 8640000)))
                : "Tiempo restante: calculando…";
        }

        private async Task<int> PbpToChdAsync(DiscJob job, string dest,
                                             string chdman, CancellationToken token)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "psxpackager.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException("Falta psxpackager.exe junto a la GUI.", exe);
            var names = LnrSuiteDiscNaming.ChdNames(job.Source, job.DiscCount);
            // A multidisc PBP is ONE job: if even one expected output exists,
            // don't create a partly overlapping collection.
            if (names.Any(name => File.Exists(Path.Combine(dest, name))))
            {
                Log("SALTADO: ya existe al menos un CHD del grupo " + job.DisplayName);
                return 0;
            }

            var stage = NewStageFolder(dest);
            try
            {
                Log("Extrayendo PBP " + job.DisplayName + " (" + job.DiscCount + " disco/s).");
                await ExecuteAsync(exe, new[] { "-i", job.Source, "-o", stage,
                                                 "-s", "-f", "%FILENAME%" }, token,
                                   job, "Extrayendo discos");
                var cues = LnrSuiteDiscNaming.ExtractedCues(stage, job.DiscCount);
                for (var i = 0; i < cues.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var outputName = names[i];
                    var provisional = Path.Combine(stage, outputName);
                    Log("Generando " + outputName + " (" + (i + 1) + "/" + cues.Count + ").");
                    await ExecuteAsync(chdman,
                        new[] { "createcd", "-i", cues[i], "-o", provisional },
                        token, job, "Creando CHD " + (i + 1) + "/" + cues.Count);
                    VerifyChdHeader(provisional);
                    await ExecuteAsync(chdman, new[] { "verify", "-i", provisional },
                                       token, job, "Verificando CHD " + (i + 1) + "/" + cues.Count);
                    VerifyChdHeader(provisional);
                }

                // Every CHD must be validated before publishing ANY file.
                token.ThrowIfCancellationRequested();
                if (!LnrSuiteDiscNaming.PublishChdGroup(stage, dest, names,
                                                          VerifyChdHeader))
                {
                    Log("SALTADO: apareció un CHD del grupo durante la conversión. " +
                        "No se publicó ningún disco nuevo.");
                    return 0;
                }
                Log("OK: " + names.Count + " CHD publicado(s) con numeración Disc N.");
                return names.Count;
            }
            finally
            {
                CleanStage(stage);
            }
        }

        /// <summary>
        /// The PSXPackager engine already extracts PBP discs into CUE and BIN.
        /// Move the complete extraction atomically into its own folder,
        /// preserving disc order and CD-Audio tracks when available.
        /// </summary>
        private async Task<int> PbpToCueAsync(DiscJob job, string dest,
                                             CancellationToken token)
        {
            var cli = Path.Combine(AppContext.BaseDirectory, "psxpackager.exe");
            if (!File.Exists(cli))
                throw new FileNotFoundException("Falta psxpackager.exe junto a la GUI.", cli);

            var name = Path.GetFileNameWithoutExtension(job.Source);
            var finalFolder = Path.Combine(dest, name);
            if (Directory.Exists(finalFolder) || File.Exists(finalFolder))
            {
                Log("SALTADO: existe salida BIN/CUE para " + name);
                return 0;
            }
            var stage = NewStageFolder(dest);
            try
            {
                Log("Extrayendo " + job.DiscCount + " disco(s) y pistas BIN/CUE de " + job.DisplayName);
                await ExecuteAsync(cli, new[] { "-i", job.Source, "-o", stage,
                                                "-s", "-f", "%FILENAME%" }, token,
                                   job, "Extrayendo BIN/CUE");

                // The original extraction may name files "Game - Disc N".
                // Rename by the actual internal disc order, rewrite ALL CUE
                // FILE references and verify the resulting pairs in staging.
                LnrSuiteDiscNaming.NormalizeBinCue(stage, job.Source, job.DiscCount);
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(finalFolder) || File.Exists(finalFolder))
                {
                    Log("SALTADO: la carpeta apareció durante la extracción: " + name);
                    return 0;
                }
                // One same-volume directory move publishes the ENTIRE group:
                // never a CUE without its BIN or some discs without others.
                var producedFiles = Directory.GetFiles(stage, "*", SearchOption.TopDirectoryOnly).Length;
                Directory.Move(stage, finalFolder);
                Log("OK: " + job.DiscCount + " disco(s) completos BIN/CUE, numerados Disc N; " +
                    producedFiles + " archivo(s) generados.");
                return producedFiles;
            }
            finally
            {
                CleanStage(stage);
            }
        }

        /// <summary>
        /// Not a full-disc reconstruction: exports only 2048-byte user data
        /// from single-track MODE1 CDs. MODE2, CD-Audio, XA Form2 and
        /// multidisc content are rejected, not silently discarded.
        /// </summary>
        private async Task<int> PbpToIsoAsync(DiscJob job, string dest,
                                             CancellationToken token)
        {
            var cli = Path.Combine(AppContext.BaseDirectory, "psxpackager.exe");
            if (!File.Exists(cli))
                throw new FileNotFoundException("Falta psxpackager.exe junto a la GUI.", cli);

            var name = Path.GetFileNameWithoutExtension(job.Source);
            var output = Path.Combine(dest, name + ".iso");
            if (File.Exists(output))
            {
                Log("SALTADO: ya existe " + Path.GetFileName(output));
                return 0;
            }

            // Recheck immediately before running the CLI: the PBP may have
            // changed since Analyze. This check is cheap (sampled sectors);
            // the ISO exporter validates every sector after extraction.
            using (var input = File.OpenRead(job.Source))
            {
                var pbp = new PbpReader(input);
                if (pbp.Discs.Count != 1)
                    throw new InvalidDataException(
                        "PBP multidisco: ISO no recomendado. Usa PBP → BIN/CUE.");
                VerifyPbpMode1(pbp.Discs[0]);
            }

            var stage = NewStageFolder(dest);
            try
            {
                Log("Comprobado PBP MODE1 de un disco; extrayendo datos: " + job.Name);
                await ExecuteAsync(cli, new[]
                {
                    "-i", job.Source, "-o", stage, "-s", "-f", "%FILENAME%"
                }, token, job, "Extrayendo PBP");

                var cues = Directory.EnumerateFiles(stage, "*.cue", SearchOption.TopDirectoryOnly).ToArray();
                if (cues.Length != 1)
                    throw new InvalidDataException(
                        "El PBP contiene varios discos o no generó un CUE válido. " +
                        "ISO no conserva multidisco: utiliza PBP → BIN/CUE.");

                var provisional = Path.Combine(stage, name + ".iso");
                job.CurrentPhase = "Creando ISO de datos";
                job.HasProgress = true;
                job.ProgressPercent = 0;
                var progress = new Progress<double>(percent =>
                {
                    if (job.Status == "Convirtiendo")
                        job.ProgressPercent = (int)Math.Clamp(Math.Round(percent), 0, 100);
                });
                var sectors = await LnrDataTrackIsoExporter.ExportAsync(
                    cues[0], provisional, token, progress);
                if (new FileInfo(provisional).Length != sectors * 2048L)
                    throw new InvalidDataException("El tamaño del ISO no coincide con los datos leídos.");

                token.ThrowIfCancellationRequested();
                if (File.Exists(output))
                {
                    Log("SALTADO: el ISO apareció durante el procesamiento.");
                    return 0;
                }
                File.Move(provisional, output);
                Log("OK: ISO de datos (" + sectors + " sectores): " + Path.GetFileName(output));
                Log("AVISO: ISO no es una imagen de CD completo para grabación; " +
                    "para PS1 usa BIN/CUE cuando quieras preservar el disco.");
                return 1;
            }
            finally
            {
                CleanStage(stage);
            }
        }

        private async Task<int> ChdToCueAsync(DiscJob job, string dest,
                                             string chdman, CancellationToken token)
        {
            var name = Path.GetFileNameWithoutExtension(job.Source);
            var final = Path.Combine(dest, name);
            if (Directory.Exists(final))
            {
                Log("SALTADO: la carpeta BIN/CUE ya existe: " + name);
                return 0;
            }

            var stage = NewStageFolder(dest);
            try
            {
                var cue = Path.Combine(stage, name + ".cue");
                var bin = Path.Combine(stage, name + ".bin");
                Log("Extrayendo CD a BIN/CUE: " + job.Name);
                await ExecuteAsync(chdman, new[] { "extractcd", "-i", job.Source,
                                                    "-o", cue, "-ob", bin }, token,
                                   job, "Extrayendo CD");
                if (!File.Exists(cue) || new FileInfo(cue).Length < 24 ||
                    !File.Exists(bin) || new FileInfo(bin).Length < 4096)
                    throw new InvalidDataException("La extracción no generó BIN y CUE válidos.");
                if (!File.ReadAllText(cue).Contains(Path.GetFileName(bin),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El CUE no referencia al BIN esperado.");
                if (Directory.Exists(final) || File.Exists(final))
                    throw new IOException("La carpeta de destino apareció durante la extracción.");
                Directory.Move(stage, final);
                Log("OK: " + Path.Combine(name, name + ".cue"));
                return 2;
            }
            finally
            {
                CleanStage(stage);
            }
        }

        private async Task<int> ChdToIsoAsync(DiscJob job, string dest,
                                             string chdman, CancellationToken token)
        {
            var name = Path.GetFileNameWithoutExtension(job.Source);
            var final = Path.Combine(dest, name + ".iso");
            if (File.Exists(final))
            {
                Log("SALTADO: " + Path.GetFileName(final) + " ya existe.");
                return 0;
            }

            var stage = NewStageFolder(dest);
            try
            {
                var iso = Path.Combine(stage, name + ".iso");
                Log("Extrayendo DVD a ISO: " + job.Name);
                await ExecuteAsync(chdman, new[] { "extractdvd", "-i", job.Source,
                                                    "-o", iso }, token,
                                   job, "Extrayendo DVD");
                var size = File.Exists(iso) ? new FileInfo(iso).Length : 0;
                if (size < 4096 || size % 2048 != 0)
                    throw new InvalidDataException("El ISO extraído está vacío o no tiene sectores de 2048 bytes.");
                if (File.Exists(final))
                    throw new IOException("La imagen ISO ya existe en destino.");
                File.Move(iso, final);
                Log("OK: " + Path.GetFileName(final));
                return 1;
            }
            finally
            {
                CleanStage(stage);
            }
        }

        private static string NewStageFolder(string target)
        {
            var stage = Path.Combine(target, ".lnr_stage_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            return stage;
        }

        private void CleanStage(string stage)
        {
            if (!Directory.Exists(stage)) return;
            try { Directory.Delete(stage, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Log("AVISO: no se pudo eliminar una carpeta temporal LNR: " + ex.Message); }
        }

        private static void VerifyChdHeader(string path)
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 4096) throw new InvalidDataException("CHD vacío o incompleto.");
            var header = new byte[8];
            if (fs.Read(header, 0, header.Length) != header.Length ||
                Encoding.ASCII.GetString(header) != "MComprHD")
                throw new InvalidDataException("Cabecera CHD no reconocible.");
        }

        private static readonly Regex SuitePercentage = new(
            @"(?<!\d)(?<percent>\d{1,3}(?:[.,]\d+)?)\s*%",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private async Task<string> ReadSuiteStreamAsync(StreamReader reader,
            DiscJob job, string phase, bool stderr)
        {
            var last = new StringBuilder();
            var lastLoggedQuarter = -1;
            while (true)
            {
                var raw = await reader.ReadLineAsync();
                if (raw == null) break;
                var line = raw.Trim();
                if (line.Length == 0) continue;

                // Limit diagnostic buffers; some CHDMAN versions emit many
                // updates and must not grow memory use with batch size.
                if (last.Length > 2500)
                    last.Remove(0, last.Length - 1300);
                last.AppendLine(line);

                if (job == null) continue;
                var percent = SuitePercentage.Match(line);
                if (percent.Success &&
                    double.TryParse(percent.Groups["percent"].Value.Replace(',', '.'),
                                    NumberStyles.Float, CultureInfo.InvariantCulture,
                                    out var value) && value >= 0 && value <= 100)
                {
                    job.HasProgress = true;
                    job.ProgressPercent = (int)Math.Round(value);
                    var quarter = job.ProgressPercent / 25;
                    if (quarter > lastLoggedQuarter && quarter > 0)
                    {
                        // Per-phase progress, not a whole-file percentage.
                        lastLoggedQuarter = quarter;
                        Log("Progreso " + job.DisplayName + ": " +
                            job.ProgressPercent + "% · " + phase + ".");
                    }
                }
                else if (line.StartsWith("Converting Disc", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Writing Disc", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Extracting Disc", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Decompressing file", StringComparison.OrdinalIgnoreCase))
                {
                    job.CurrentPhase = phase + " · " + line.Split(':')[0];
                    job.HasProgress = false;
                    lastLoggedQuarter = -1;
                }
                else if (stderr && (line.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith("Fatal", StringComparison.OrdinalIgnoreCase)))
                {
                    Log("MOTOR: " + line);
                }
            }
            return last.ToString();
        }

        private async Task ExecuteAsync(string exe, IEnumerable<string> arguments,
                                        CancellationToken token, DiscJob job = null,
                                        string phase = null)
        {
            token.ThrowIfCancellationRequested();
            if (job != null)
            {
                job.CurrentPhase = phase ?? "Procesando";
                job.HasProgress = false;
                job.ProgressPercent = 0;
            }
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            foreach (var arg in arguments) start.ArgumentList.Add(arg);
            using var proc = new Process { StartInfo = start };
            Task<string> stdout = null, stderr = null;
            try
            {
                if (!proc.Start())
                    throw new InvalidOperationException("No se pudo iniciar: " + Path.GetFileName(exe));
                _activeProcesses.Add(proc);
                stdout = ReadSuiteStreamAsync(proc.StandardOutput, job, phase ?? "Procesando", false);
                stderr = ReadSuiteStreamAsync(proc.StandardError, job, phase ?? "Procesando", true);
                await proc.WaitForExitAsync(token);
                var outputs = await Task.WhenAll(stdout, stderr);
                token.ThrowIfCancellationRequested();
                if (proc.ExitCode != 0)
                {
                    var detail = string.IsNullOrWhiteSpace(outputs[1]) ? outputs[0] : outputs[1];
                    detail = detail.Length > 900 ? detail.Substring(detail.Length - 900) : detail;
                    throw new InvalidOperationException(Path.GetFileName(exe) + " devolvió " +
                                                        proc.ExitCode + ": " + detail.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                try { await proc.WaitForExitAsync(); }
                catch (InvalidOperationException) { }
                throw;
            }
            finally
            {
                _activeProcesses.Remove(proc);
                // The child has exited (normally or by cancellation) before
                // any staging directory can be cleaned by the caller.
                if (stdout != null && stderr != null)
                {
                    try { await Task.WhenAll(stdout, stderr); }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_running || _cancelRequested) return;
            _cancelRequested = true;
            _pauseRequested = false;
            _pauseComplete = false;
            _cancel?.Cancel();
            _resumeGate?.TrySetResult(true);
            CancelButton.IsEnabled = false;
            RefreshConvertButton();
            Log("CANCELANDO: deteniendo " + _activeProcesses.Count +
                " motor(es) activo(s). Originales conservados.");
            foreach (var process in _activeProcesses.ToArray())
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                catch (Win32Exception ex)
                {
                    Log("AVISO: no se pudo detener un proceso: " + ex.Message);
                }
            }
        }

        private void BeginShutdownCountdown()
        {
            if (_shutdownPending) return;
            _shutdownPending = true;
            _shutdownSecondsRemaining = 60;
            ShutdownCheck.IsChecked = true;
            ShutdownCheck.IsEnabled = false;
            ShutdownCheck.Content = $"Apagado de Windows en {_shutdownSecondsRemaining} s";
            CancelScheduledShutdownButton.Visibility = Visibility.Visible;
            CancelScheduledShutdownButton.IsEnabled = true;
            Log("APAGADO OPCIONAL: el lote terminó sin errores, con archivos nuevos.");
            Log("Tienes 60 segundos para cancelar. Windows intentará un apagado normal, sin forzar aplicaciones.");
            SaveLog();
            _shutdownCountdown.Start();
        }

        private void CancelScheduledShutdown_OnClick(object sender, RoutedEventArgs e) =>
            CancelShutdownCountdown("Apagado cancelado por el usuario.");

        private void CancelShutdownCountdown(string reason)
        {
            if (!_shutdownPending) return;
            _shutdownCountdown.Stop();
            _shutdownPending = false;
            _shutdownSecondsRemaining = 0;
            CancelScheduledShutdownButton.Visibility = Visibility.Collapsed;
            ShutdownCheck.Content = "Apagar equipo al finalizar el lote";
            ShutdownCheck.IsChecked = false;
            ShutdownCheck.IsEnabled = !_running;
            Log(reason);
            SaveLog();
        }

        private void ShutdownCountdown_OnTick(object sender, EventArgs e)
        {
            if (!_shutdownPending)
            {
                _shutdownCountdown.Stop();
                return;
            }
            _shutdownSecondsRemaining--;
            if (_shutdownSecondsRemaining > 0)
            {
                ShutdownCheck.Content = $"Apagado de Windows en {_shutdownSecondsRemaining} s";
                return;
            }
            _shutdownCountdown.Stop();
            _shutdownPending = false;
            CancelScheduledShutdownButton.Visibility = Visibility.Collapsed;
            ShutdownCheck.IsChecked = false;
            ShutdownCheck.Content = "Apagar equipo al finalizar el lote";
            ShutdownCheck.IsEnabled = !_running;

            Log("Cuenta atrás terminada: solicitando apagado normal de Windows.");
            SaveLog();
            try
            {
                var executable = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
                var info = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                info.ArgumentList.Add("/s");
                info.ArgumentList.Add("/t");
                info.ArgumentList.Add("0"); // Never /f: don't force-close applications.
                using var process = Process.Start(info);
                if (process == null)
                    throw new InvalidOperationException("Windows no pudo iniciar shutdown.exe.");
                if (process.WaitForExit(3000) && process.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"Windows rechazó el apagado. Código: {process.ExitCode}.");
                Log("Solicitud enviada; Windows puede posponer el apagado si hay documentos sin guardar.");
                SaveLog();
            }
            catch (Exception ex) when (ex is Win32Exception or IOException
                                       or UnauthorizedAccessException or InvalidOperationException)
            {
                Log("ERROR: no se pudo solicitar el apagado: " + ex.Message);
                SaveLog();
                MessageBox.Show(this, "No se pudo apagar el equipo automáticamente.\n\n" + ex.Message,
                                "Apagado no iniciado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            if (!_running)
            {
                if (_shutdownPending)
                    CancelShutdownCountdown("Apagado automático cancelado al cerrar la Suite de discos.");
                SavePreferences();
                return;
            }
            e.Cancel = true;
            MessageBox.Show(this,
                "Hay una conversión en curso. Pulsa Cancelar y cierra esta ventana al finalizar.",
                "Conversión activa", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public sealed class DiscJob : INotifyPropertyChanged
        {
            private string _status = "Pendiente";
            private string _outputCount = "—";
            private string _phase = "Iniciando";
            private int _progressPercent;
            private bool _hasProgress;
            private long? _outputBytes;
            private TimeSpan? _elapsed;

            public event PropertyChangedEventHandler PropertyChanged;

            public DiscJob(string source, string relativeFolder, long sourceBytes, int mode)
            {
                Source = source;
                RelativeFolder = relativeFolder ?? "";
                SourceBytes = sourceBytes;
                Mode = mode;
                Name = Path.GetFileName(source);
            }

            public string Source { get; }
            public string RelativeFolder { get; }
            public string Name { get; }
            public string DisplayName => Path.Combine(RelativeFolder, Name);
            public long SourceBytes { get; }
            public int Mode { get; }
            public int DiscCount { get; set; } = 1;
            public string PlanError { get; set; }
            public string[] PlannedOutputs { get; set; } = Array.Empty<string>();
            public string SizeText => FormatSize(SourceBytes);

            public string Status
            {
                get => _status;
                set
                {
                    if (_status == value) return;
                    _status = value;
                    Notify(nameof(Status));
                    Notify(nameof(DisplayStatus));
                    Notify(nameof(StatusBrush));
                }
            }
            public string CurrentPhase
            {
                get => _phase;
                set
                {
                    if (_phase == value) return;
                    _phase = value;
                    Notify(nameof(DisplayStatus));
                }
            }
            public bool HasProgress
            {
                get => _hasProgress;
                set
                {
                    if (_hasProgress == value) return;
                    _hasProgress = value;
                    Notify(nameof(IsProgressIndeterminate));
                    Notify(nameof(DisplayStatus));
                }
            }
            public int ProgressPercent
            {
                get => _progressPercent;
                set
                {
                    if (_progressPercent == value) return;
                    _progressPercent = value;
                    Notify(nameof(ProgressPercent));
                    Notify(nameof(DisplayStatus));
                }
            }
            public bool IsProgressIndeterminate => !HasProgress;
            public System.Windows.Media.Brush StatusBrush => Status == "ERROR"
                ? System.Windows.Media.Brushes.Firebrick
                : Status == "Completado" ? System.Windows.Media.Brushes.ForestGreen
                : System.Windows.Media.Brushes.DimGray;
            public string DisplayStatus => Status == "Convirtiendo"
                ? CurrentPhase + (HasProgress ? " " + ProgressPercent + "%" : "")
                : Status == "ERROR" && !string.IsNullOrWhiteSpace(PlanError)
                    ? "ERROR: " + PlanError : Status;

            public long? OutputBytes
            {
                get => _outputBytes;
                set
                {
                    if (_outputBytes == value) return;
                    _outputBytes = value;
                    Notify(nameof(OutputSizeText));
                    Notify(nameof(VariationText));
                    Notify(nameof(VariationTooltip));
                    Notify(nameof(VariationBrush));
                }
            }
            public string OutputSizeText => OutputBytes.HasValue
                ? FormatSize(OutputBytes.Value) : "—";
            public long? Difference => OutputBytes.HasValue
                ? OutputBytes.Value - SourceBytes : null;
            public string VariationText
            {
                get
                {
                    // An ISO omits CD-specific information, so never present
                    // its smaller size as a compression benefit.
                    if (Mode == 2) return "No comparable";
                    if (!Difference.HasValue) return "—";
                    var value = Difference.Value;
                    return (value > 0 ? "+" : "") + FormatSize(value);
                }
            }
            public string VariationTooltip => Mode == 2
                ? "ISO extrae solo datos compatibles; no es un formato completo de preservación del CD."
                : "Tamaño de salida menos tamaño de origen. No implica mayor calidad ni compatibilidad.";
            public System.Windows.Media.Brush VariationBrush => Mode == 2
                ? System.Windows.Media.Brushes.DimGray
                : Difference > 0 ? System.Windows.Media.Brushes.Firebrick
                : Difference < 0 ? System.Windows.Media.Brushes.ForestGreen
                : System.Windows.Media.Brushes.DimGray;

            public TimeSpan? Elapsed
            {
                get => _elapsed;
                set
                {
                    if (_elapsed == value) return;
                    _elapsed = value;
                    Notify(nameof(ElapsedText));
                }
            }
            public string ElapsedText => Elapsed.HasValue
                ? (Elapsed.Value.TotalHours >= 1
                    ? Elapsed.Value.ToString(@"h\:mm\:ss")
                    : Elapsed.Value.ToString(@"mm\:ss"))
                : "—";

            public string OutputCount
            {
                get => _outputCount;
                set
                {
                    if (_outputCount == value) return;
                    _outputCount = value;
                    Notify(nameof(OutputCount));
                }
            }
            private void Notify(string property) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
