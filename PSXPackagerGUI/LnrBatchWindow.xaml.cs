using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PSXPackagerGUI
{
    /// <summary>
    /// LNR workflow around the upstream command-line program, without changing
    /// the original GUI's single-disc/batch processing code.
    /// </summary>
    public partial class LnrBatchWindow : Window
    {
        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
        private static readonly Regex CliProgress = new Regex(
            @"^Progress:\s*(?<value>\d{1,3})\s*%",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex CliPhase = new Regex(
            @"^(Converting Disc|Writing Disc|Extracting Disc|Decompressing file)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private readonly ObservableCollection<LnrConversionJob> _jobs = new();
        private readonly List<string> _messages = new();
        private CancellationTokenSource _cancel;
        private Process _activeProcess;
        private bool _running;
        private string _planFolder;
        private int _planMode;
        private long _sumOriginal;
        private long _sumConverted;
        private int _succeeded;
        private int _skipped;
        private int _failed;
        private readonly LnrGuiPreferences _preferences;
        private readonly DispatcherTimer _batchClock = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        private Stopwatch _activeJobClock;
        private long _calibratedBytes;
        private double _calibratedSeconds;
        private bool _batchCanceled;

        public LnrBatchWindow()
        {
            InitializeComponent();
            Title = "PSXPackagerLNR · " + LnrVersionInfo.Display + " | CHD a PBP";
            JobList.ItemsSource = _jobs;
            _batchClock.Tick += (sender, e) => UpdateBatchProgress();
            _preferences = LnrGuiPreferences.Load();

            var current = new DirectoryInfo(AppContext.BaseDirectory);
            var portableSource = current.Parent?.Name.Equals(".tools", StringComparison.OrdinalIgnoreCase) == true
                ? current.Parent.Parent?.FullName : null;
            var defaultFolder = portableSource ??
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            SourceBox.Text = Directory.Exists(_preferences.LastSourceFolder)
                ? _preferences.LastSourceFolder : defaultFolder;

            ModeCombo.SelectedIndex = _preferences.MultidiscMode is 0 or 1
                ? _preferences.MultidiscMode : 0;
            LevelCombo.SelectedIndex = _preferences.CompressionIndex is >= 0 and <= 2
                ? _preferences.CompressionIndex : 1;
            AutoScrollCheck.IsChecked = _preferences.FollowActivity;
            if (_preferences.WindowWidth >= MinWidth && _preferences.WindowWidth <= SystemParameters.WorkArea.Width)
                Width = _preferences.WindowWidth;
            if (_preferences.WindowHeight >= MinHeight && _preferences.WindowHeight <= SystemParameters.WorkArea.Height)
                Height = _preferences.WindowHeight;
            if (_preferences.LogPanelHeight >= 150 && _preferences.LogPanelHeight <= 600)
                LogPanelRow.Height = new GridLength(_preferences.LogPanelHeight);

            Append("PSXPackagerLNR " + LnrVersionInfo.Display);
            Append("Selecciona una carpeta con archivos CHD y pulsa Analizar.");
            Append("La barra de cada juego indica el progreso de la fase actual; la inferior indica el avance del lote.");
        }

        private void SavePreferences()
        {
            try
            {
                LnrGuiPreferences.Save(new LnrGuiPreferences
                {
                    LastSourceFolder = SourceBox.Text.Trim().Trim('"'),
                    MultidiscMode = ModeCombo.SelectedIndex,
                    CompressionIndex = LevelCombo.SelectedIndex,
                    FollowActivity = AutoScrollCheck.IsChecked == true,
                    WindowWidth = ActualWidth,
                    WindowHeight = ActualHeight,
                    LogPanelHeight = LogPanelRow.ActualHeight
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Append("AVISO: no se pudieron guardar las preferencias: " + ex.Message);
            }
        }

        private void Append(string message)
        {
            // Split multiline notices so every informational phrase gets its own line.
            foreach (var part in (message ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var entry = $"[{DateTime.Now:HH:mm:ss}] {part.TrimEnd()}";
                _messages.Add(entry);
                OutputBox.AppendText(entry + Environment.NewLine);
            }
            if (OutputBox.Text.Length > 160000)
                OutputBox.Text = OutputBox.Text.Substring(OutputBox.Text.Length - 120000);
            if (AutoScrollCheck.IsChecked == true)
                OutputBox.ScrollToEnd();
        }

        private static string Size(long bytes) => (bytes / 1048576.0).ToString("N2", Es) + " MB";

        private bool IsMerged => ModeCombo.SelectedIndex == 1;

        private int SelectedLevel => int.Parse(((ComboBoxItem)LevelCombo.SelectedItem).Tag.ToString(),
                                               CultureInfo.InvariantCulture);

        private bool _firstLoadScanned;

        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_firstLoadScanned) return;
            _firstLoadScanned = true;
            TryAutoScan(force: true);
        }

        private void LnrAbout_OnClick(object sender, RoutedEventArgs e)
        {
            new LnrAboutWindow { Owner = this }.ShowDialog();
        }

        private void Browse_OnClick(object sender, RoutedEventArgs e)
        {
            // The default Windows folder dialog hides files. Our picker
            // previews all CHD games before the folder is selected.
            var dialog = new LnrFolderPickerWindow(SourceBox.Text) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                SourceBox.Text = dialog.SelectedFolder;
                TryAutoScan(force: true);
            }
        }

        private void SourceBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            TryAutoScan(force: true);
            e.Handled = true;
        }

        private void SourceBox_OnLostKeyboardFocus(object sender,
            System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            // A pasted path should populate the table without an extra click.
            if (IsLoaded) TryAutoScan(force: false);
        }

        private void ModeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Rebuild grouping immediately when changing separated/merged mode.
            // IsLoaded prevents re-entrancy during InitializeComponent.
            if (IsLoaded && !_running) TryAutoScan(force: true);
        }

        private void TryAutoScan(bool force)
        {
            if (_running || !IsLoaded) return;
            var folder = SourceBox.Text.Trim().Trim('"');
            if (!Directory.Exists(folder))
            {
                _jobs.Clear();
                _planFolder = null;
                CountLabel.Text = "Carpeta no disponible";
                RunButton.IsEnabled = false;
                return;
            }
            if (!force && _planFolder != null
                && string.Equals(Path.GetFullPath(folder), _planFolder, StringComparison.OrdinalIgnoreCase)
                && _planMode == ModeCombo.SelectedIndex)
                return;
            try
            {
                Scan();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or InvalidOperationException or ArgumentException)
            {
                _jobs.Clear();
                _planFolder = null;
                CountLabel.Text = "0 juegos CHD";
                RunButton.IsEnabled = false;
                SummaryLabel.Text = "Selecciona una carpeta con archivos CHD.";
                Append("AVISO: No se pudieron cargar los juegos de la carpeta: " + ex.Message);
            }
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

        private void OpenPbp_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
                var output = Path.Combine(folder, "PBP");
                if (!Directory.Exists(output))
                {
                    MessageBox.Show(this, "Todavía no existe una carpeta PBP en la ruta seleccionada. " +
                                    "Se creará al empezar una conversión.",
                                    "Carpeta de salida", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Process.Start(new ProcessStartInfo { FileName = output, UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or Win32Exception)
            {
                MessageBox.Show(this, "No se pudo abrir la carpeta: " + ex.Message,
                                "Carpeta de salida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Scan_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Scan();
            }
            catch (Exception ex)
            {
                Append("ERROR al analizar: " + ex.Message);
                MessageBox.Show(this, ex.Message, "No se pudo analizar",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Scan()
        {
            var folder = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("No existe la carpeta: " + folder);

            _jobs.Clear();
            if (!_running)
            {
                // A refreshed plan is a new batch, not a continuation of
                // the previous run's statistics or ETA calibration.
                _succeeded = _skipped = _failed = 0;
                _calibratedBytes = 0;
                _calibratedSeconds = 0;
                _batchCanceled = false;
            }
            var outputFolder = Path.Combine(folder, "PBP");
            var existing = 0;
            foreach (var job in Plan(folder, IsMerged))
            {
                var foundPbp = Path.Combine(outputFolder, job.Name + ".pbp");
                if (File.Exists(foundPbp))
                {
                    job.ReadExistingPbp(foundPbp);
                    job.Status = "Saltado / Ya existe";
                    existing++;
                }
                _jobs.Add(job);
            }

            _planFolder = folder;
            _planMode = ModeCombo.SelectedIndex;
            CountLabel.Text = existing == 0
                ? $"{_jobs.Count} PBP previstos"
                : $"{_jobs.Count} PBP previstos · {existing} ya existen";
            RunButton.IsEnabled = _jobs.Count > 0;
            SummaryLabel.Text = "Salida: " + outputFolder + " · originales intactos.";
            Append($"Plan preparado: {_jobs.Count} PBP / modo {(IsMerged ? "UNIDOS" : "SEPARADOS")}, compresión {SelectedLevel}.");
            if (existing > 0)
                Append($"AVISO: {existing} archivos ya están en PBP y serán saltados sin sobrescribir.");
            UpdateBatchProgress();
        }

        private IEnumerable<LnrConversionJob> Plan(string folder, bool merge)
        {
            var files = Directory.EnumerateFiles(folder, "*.chd", SearchOption.TopDirectoryOnly)
                                 .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                 .ToList();

            if (files.Count == 0)
                throw new InvalidOperationException("No hay archivos .chd en la carpeta seleccionada.");

            var byName = files.ToDictionary(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<LnrConversionJob>();

            if (merge)
            {
                foreach (var playlist in Directory.EnumerateFiles(folder, "*.m3u", SearchOption.TopDirectoryOnly)
                                                  .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    var found = new List<string>();
                    var errors = new List<string>();
                    foreach (var line in File.ReadAllLines(playlist))
                    {
                        var entry = line.Trim().Trim('\uFEFF').Trim('"');
                        if (entry.Length == 0 || entry.StartsWith("#")) continue;
                        var leaf = Path.GetFileName(entry);
                        if (!string.Equals(leaf, entry, StringComparison.Ordinal))
                        {
                            errors.Add("Ruta fuera de la carpeta: " + entry);
                            continue;
                        }
                        if (Path.GetExtension(leaf).Equals(".cue", StringComparison.OrdinalIgnoreCase))
                            leaf = Path.ChangeExtension(leaf, ".chd");
                        if (!Path.GetExtension(leaf).Equals(".chd", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add("Extensión no admitida: " + entry);
                            continue;
                        }
                        if (byName.TryGetValue(leaf, out var full))
                            found.Add(full);
                        else
                            errors.Add("Falta " + leaf);
                    }

                    bool valid = errors.Count == 0
                                 && found.Count >= 2 && found.Count <= 5
                                 && found.Distinct(StringComparer.OrdinalIgnoreCase).Count() == found.Count
                                 && found.All(f => !claimed.Contains(f) && !blocked.Contains(f));

                    if (!valid)
                    {
                        foreach (var disc in found) blocked.Add(disc);
                        Append("AVISO: M3U incompleto o ambiguo, omitido: "
                               + Path.GetFileName(playlist) + ". " + string.Join("; ", errors));
                        continue;
                    }

                    foreach (var disc in found) claimed.Add(disc);
                    result.Add(new LnrConversionJob(Path.GetFileNameWithoutExtension(playlist),
                                                    found, "M3U"));
                }

                var grouped = new Dictionary<string, List<(int number, string path)>>(StringComparer.OrdinalIgnoreCase);
                var discSuffix = new Regex(
                    @"^(?<title>.+?)\s*[-_.]?\s*\(?\s*(?:CD|Disc|Disk)\s*[-_.]?\s*(?<n>[1-5])\s*\)?$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                foreach (var file in files.Where(f => !claimed.Contains(f) && !blocked.Contains(f)))
                {
                    var match = discSuffix.Match(Path.GetFileNameWithoutExtension(file));
                    if (!match.Success) continue;
                    var title = match.Groups["title"].Value.Trim(' ', '-', '_', '.');
                    if (title.Length == 0) continue;
                    if (!grouped.TryGetValue(title, out var entries))
                    {
                        entries = new List<(int number, string path)>();
                        grouped[title] = entries;
                    }
                    entries.Add((int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture), file));
                }

                foreach (var group in grouped.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var discs = group.Value.OrderBy(e => e.number).ToArray();
                    if (discs.Length < 2 || discs.Length > 5) continue;
                    if (!discs.Select((d, i) => d.number == i + 1).All(v => v))
                    {
                        Append("AVISO: serie de discos no consecutiva: " + group.Key + "; se tratará por separado.");
                        continue;
                    }
                    var paths = discs.Select(d => d.path).ToList();
                    foreach (var path in paths) claimed.Add(path);
                    result.Add(new LnrConversionJob(group.Key, paths, "Auto"));
                }
            }

            foreach (var file in files.Where(f => !claimed.Contains(f) && !blocked.Contains(f)))
            {
                result.Add(new LnrConversionJob(Path.GetFileNameWithoutExtension(file),
                                                new List<string> { file }, "Individual"));
            }

            var counts = result.GroupBy(job => job.Name + ".pbp", StringComparer.OrdinalIgnoreCase)
                               .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (var duplicate in counts.Where(pair => pair.Value > 1))
                Append("AVISO: nombre PBP duplicado: " + duplicate.Key + ". Se excluye para evitar sobrescrituras.");

            return result.Where(job => counts[job.Name + ".pbp"] == 1)
                         .OrderBy(job => job.Name, StringComparer.OrdinalIgnoreCase);
        }

        private async void Run_OnClick(object sender, RoutedEventArgs e)
        {
            if (_running) return;
            try
            {
                var folder = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
                if (_jobs.Count == 0 || _planFolder == null ||
                    !string.Equals(folder, _planFolder, StringComparison.OrdinalIgnoreCase) ||
                    _planMode != ModeCombo.SelectedIndex)
                    Scan();

                string cli = Path.Combine(AppContext.BaseDirectory, "psxpackager.exe");
                if (!File.Exists(cli))
                {
                    MessageBox.Show(this,
                        "No se encuentra psxpackager.exe junto a la interfaz gráfica. " +
                        "Utiliza el ZIP GUI compilado para PSXPackagerLNR, que debe incluir también la CLI.",
                        "Falta el ejecutable de conversión", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (_jobs.Count == 0) return;
                var answer = MessageBox.Show(this,
                    $"Se van a convertir {_jobs.Count} juegos a PBP.\n\n" +
                    $"Modo multidisco: {(IsMerged ? "Unidos" : "Separados")}\n" +
                    $"Compresión: {SelectedLevel}\n" +
                    $"Destino: {Path.Combine(folder, "PBP")}\n\n" +
                    "Los CHD originales NO se modificarán.\n" +
                    "Los PBP existentes se omitirán.\n\n" +
                    "¿Iniciar la conversión?",
                    "Confirmar conversión LNR", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;

                _running = true;
                _cancel = new CancellationTokenSource();
                SetControls(false);
                _sumOriginal = _sumConverted = 0;
                _succeeded = _skipped = _failed = 0;
                _calibratedBytes = 0;
                _calibratedSeconds = 0;
                _batchCanceled = false;
                _activeJobClock = null;
                WorkProgress.Value = 0;
                foreach (var job in _jobs)
                {
                    job.ProgressPercent = 0;
                    job.CurrentPhase = "Iniciando";
                    job.Elapsed = null;
                    var target = Path.Combine(folder, "PBP", job.Name + ".pbp");
                    if (File.Exists(target))
                    {
                        job.ReadExistingPbp(target);
                        job.Status = "Saltado / Ya existe";
                    }
                    else
                    {
                        job.OutputBytes = null;
                        job.Status = "Pendiente";
                    }
                }
                UpdateBatchProgress();
                _batchClock.Start();
                await ConvertAllAsync(cli, folder, SelectedLevel, _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                _batchCanceled = true;
                Append("Conversión cancelada a petición del usuario.");
            }
            catch (Exception ex)
            {
                Append("ERROR GENERAL: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Error de conversión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _batchClock.Stop();
                _activeJobClock = null;
                _running = false;
                UpdateBatchProgress();
                _activeProcess = null;
                _cancel?.Dispose();
                _cancel = null;
                SetControls(true);
                SaveLog();
            }
        }

        private void SetControls(bool idle)
        {
            RunButton.IsEnabled = idle && _jobs.Count > 0;
            ScanButton.IsEnabled = idle;
            BrowseButton.IsEnabled = idle;
            ModeCombo.IsEnabled = idle;
            LevelCombo.IsEnabled = idle;
            SourceBox.IsEnabled = idle;
            CancelButton.IsEnabled = !idle;
        }

        private async Task ConvertAllAsync(string cli, string folder, int level, CancellationToken cancel)
        {
            var dest = Path.Combine(folder, "PBP");
            Directory.CreateDirectory(dest);
            var total = _jobs.Count;
            int processed = 0;

            foreach (var job in _jobs)
            {
                cancel.ThrowIfCancellationRequested();
                var finalPath = Path.Combine(dest, job.Name + ".pbp");
                var stage = Path.Combine(Path.GetTempPath(), "PSXPackagerLNR",
                                         Guid.NewGuid().ToString("N"));
                Stopwatch timer = null;
                try
                {
                    if (File.Exists(finalPath))
                    {
                        job.ReadExistingPbp(finalPath);
                        job.Status = "Saltado / Ya existe";
                        _skipped++;
                        Append("SALTADO: " + job.Name + ".pbp ya existe en la carpeta PBP; no se sobrescribe.");
                        continue;
                    }

                    timer = Stopwatch.StartNew();
                    _activeJobClock = timer;
                    Directory.CreateDirectory(stage);
                    var input = job.Discs[0];
                    if (job.Discs.Count > 1)
                    {
                        input = Path.Combine(stage, job.Name + ".m3u");
                        // Absolute paths avoid assuming that CHDs live beside the temporary M3U.
                        File.WriteAllLines(input, job.Discs, new UTF8Encoding(false));
                    }
                    job.ProgressPercent = 0;
                    job.Status = "Convirtiendo";
                    Append($"[{processed + 1}/{total}] {job.Name} ({job.Discs.Count} disco/s)");
                    var start = new ProcessStartInfo(cli)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = AppContext.BaseDirectory
                    };
                    foreach (var arg in new[] { "-i", input, "-o", stage, "-l",
                                                level.ToString(CultureInfo.InvariantCulture),
                                                "-s", "-f", "%FILENAME%" })
                        start.ArgumentList.Add(arg);

                    using var process = new Process { StartInfo = start };
                    _activeProcess = process;
                    if (!process.Start()) throw new InvalidOperationException("No se pudo iniciar la CLI.");
                    // Read stdout and stderr while conversion is running.
                    // The CLI emits "Progress: NN%" for each processing phase.
                    var outputTask = ReadCliStreamAsync(process.StandardOutput, job, false);
                    var errorTask = ReadCliStreamAsync(process.StandardError, job, true);
                    try
                    {
                        await process.WaitForExitAsync(cancel);
                        var outputs = await Task.WhenAll(outputTask, errorTask);
                        if (process.ExitCode != 0)
                        {
                            var detail = !string.IsNullOrWhiteSpace(outputs[1]) ? outputs[1] : outputs[0];
                            throw new InvalidOperationException(
                                $"psxpackager.exe devolvió {process.ExitCode}. " + detail.Trim());
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited) process.Kill(entireProcessTree: true);
                        throw;
                    }
                    finally
                    {
                        _activeProcess = null;
                    }

                    var produced = Directory.GetFiles(stage, "*.pbp", SearchOption.TopDirectoryOnly);
                    if (produced.Length != 1) throw new InvalidDataException(
                        $"Se esperaba un PBP; encontrados: {produced.Length}.");
                    var pbp = produced[0];
                    using (var stream = File.OpenRead(pbp))
                    {
                        var header = new byte[4];
                        if (stream.Length < 4096 ||
                            stream.Read(header, 0, 4) != 4 ||
                            header[0] != 0 || header[1] != 0x50 ||
                            header[2] != 0x42 || header[3] != 0x50)
                            throw new InvalidDataException("No se ha generado un PBP con cabecera reconocible.");
                    }
                    if (File.Exists(finalPath))
                        throw new IOException("El PBP de destino apareció durante la conversión.");
                    File.Move(pbp, finalPath);
                    var sourceBytes = job.SourceBytes ?? job.Discs.Sum(path => new FileInfo(path).Length);
                    var newBytes = new FileInfo(finalPath).Length;
                    job.OutputBytes = newBytes;
                    timer.Stop();
                    job.Elapsed = timer.Elapsed;
                    _calibratedBytes += sourceBytes;
                    _calibratedSeconds += Math.Max(0.1, timer.Elapsed.TotalSeconds);
                    _sumOriginal += sourceBytes;
                    _sumConverted += newBytes;
                    _succeeded++;
                    job.ProgressPercent = 100;
                    job.Status = "Completado";
                    Append($"OK: {Size(sourceBytes)} CHD → {Size(newBytes)} PBP; variación PBP vs CHD {job.VariationText}; tiempo {job.ElapsedText}.");

                    foreach (var disc in job.Discs)
                    {
                        var sbi = Path.ChangeExtension(disc, ".sbi");
                        if (!File.Exists(sbi)) continue;
                        try
                        {
                            var copy = Path.Combine(dest, Path.GetFileName(sbi));
                            if (!File.Exists(copy)) File.Copy(sbi, copy);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            // The PBP was already converted correctly: an optional
                            // external SBI failure must not mark it as a broken PBP.
                            Append("AVISO: PBP completado, pero no se pudo copiar SBI: " + ex.Message);
                        }
                        if (job.Discs.Count > 1)
                            Append("AVISO: SBI externo de juego multidisco. Comprueba soporte en tu emulador.");
                    }
                }
                catch (OperationCanceledException)
                {
                    job.Status = "Cancelado";
                    throw;
                }
                catch (Exception ex)
                {
                    _failed++;
                    job.Status = "ERROR";
                    Append("ERROR " + job.Name + ": " + ex.Message);
                }
                finally
                {
                    if (timer != null)
                    {
                        timer.Stop();
                        job.Elapsed ??= timer.Elapsed;
                        _activeJobClock = null;
                    }
                    if (Directory.Exists(stage))
                    {
                        try { Directory.Delete(stage, recursive: true); }
                        catch (Exception ex) { Append("AVISO: no se pudo borrar el temporal: " + ex.Message); }
                    }
                    processed++;
                    UpdateBatchProgress();
                }
            }

            Append($"FIN. Correctos: {_succeeded}, omitidos: {_skipped}, errores: {_failed}.");
            if (_sumOriginal > 0)
            {
                var difference = _sumConverted - _sumOriginal;
                var sign = difference > 0 ? "+" : "";
                var percent = 100.0 * difference / _sumOriginal;
                Append($"Total convertido: {Size(_sumOriginal)} CHD → {Size(_sumConverted)} PBP.");
                Append($"Variación total PBP respecto a CHD: {sign}{Size(difference)} " +
                       $"({(percent > 0 ? "+" : "")}{percent.ToString("N2", Es)} %).");
            }
        }

        private static string ReadableTime(TimeSpan span)
        {
            if (span.TotalSeconds < 1) return "menos de 1 s";
            return span.TotalHours >= 1
                ? span.ToString(@"h\:mm\:ss")
                : span.ToString(@"mm\:ss");
        }

        private void UpdateBatchProgress()
        {
            var total = _jobs.Count;
            // Count files actually processed by this run. Files already
            // present are premarked during scan but not yet counted until
            // the batch loop skips them.
            var completed = _succeeded + _skipped + _failed;

            // Only finished games count. Never misuse the CLI's per-phase
            // percentage as a percentage of the whole batch.
            var overall = total == 0 ? 0.0 : 100.0 * completed / total;
            WorkProgress.Value = overall;
            BatchPercentLabel.Text = $"Avance del lote: {overall.ToString("N0", Es)} % ({completed}/{total})";

            if (_running || completed > 0)
                SummaryLabel.Text = $"Correctos: {_succeeded} · Saltados: {_skipped} · Errores: {_failed}";
            if (!_running)
            {
                EtaLabel.Text = _batchCanceled ? "Lote cancelado"
                    : total > 0 && completed >= total ? "Lote finalizado" : "Tiempo restante: sin calcular";
                return;
            }

            if (completed >= total)
            {
                EtaLabel.Text = "Lote finalizado";
                return;
            }
            if (_calibratedBytes <= 0 || _calibratedSeconds <= 0)
            {
                EtaLabel.Text = "Tiempo restante: calculando tras el primer juego…";
                return;
            }

            // Calibrate from actual completed conversions. Input CHD sizes
            // account for very unequal games better than game-count average.
            var secondsPerByte = _calibratedSeconds / _calibratedBytes;
            double remaining = 0;
            foreach (var job in _jobs)
            {
                if (job.Status != "Pendiente" && job.Status != "Convirtiendo")
                    continue;

                if (!job.SourceBytes.HasValue || job.SourceBytes.Value <= 0)
                {
                    remaining += _calibratedSeconds / Math.Max(1, _succeeded);
                    continue;
                }

                var expectedSeconds = job.SourceBytes.Value * secondsPerByte;
                if (job.Status == "Convirtiendo" && _activeJobClock != null)
                {
                    var left = expectedSeconds - _activeJobClock.Elapsed.TotalSeconds;
                    // When a game runs substantially slower than previous ones,
                    // avoid falsely promising zero seconds remaining.
                    if (left <= 0)
                    {
                        EtaLabel.Text = "Tiempo restante: recalculando (juego más lento)…";
                        return;
                    }
                    remaining += left;
                }
                else
                    remaining += expectedSeconds;
            }

            if (double.IsNaN(remaining) || double.IsInfinity(remaining))
            {
                EtaLabel.Text = "Tiempo restante: calculando…";
                return;
            }

            EtaLabel.Text = "Tiempo restante estimado: ~" +
                            ReadableTime(TimeSpan.FromSeconds(Math.Min(remaining, TimeSpan.FromDays(99).TotalSeconds)));
        }

        private async Task<string> ReadCliStreamAsync(StreamReader reader, LnrConversionJob job, bool isError)
        {
            var captured = new StringBuilder();
            int lastLoggedQuarter = -1;
            while (true)
            {
                // WPF synchronization context keeps bound properties and log on UI thread.
                var raw = await reader.ReadLineAsync();
                if (raw == null) break;
                var line = raw.Trim();
                if (line.Length == 0) continue;
                captured.AppendLine(line);

                if (isError)
                {
                    Append("CLI ERROR: " + line);
                    continue;
                }

                var progress = CliProgress.Match(line);
                if (progress.Success &&
                    int.TryParse(progress.Groups["value"].Value, out var percent))
                {
                    percent = Math.Clamp(percent, 0, 100);
                    job.ProgressPercent = percent;
                    var quarter = percent / 25;
                    if (percent == 100 || (quarter > lastLoggedQuarter && quarter > 0))
                    {
                        Append($"Progreso {job.Name}: {percent}% (fase actual).");
                        lastLoggedQuarter = quarter;
                    }
                    continue;
                }

                var phase = CliPhase.Match(line);
                if (phase.Success)
                {
                    // Per-phase progress is not the same as completed-file percentage.
                    lastLoggedQuarter = -1;
                    job.ProgressPercent = 0;
                    var phaseName = phase.Groups[1].Value;
                    job.CurrentPhase = phaseName.ToLowerInvariant() switch
                    {
                        "converting disc" => "Convirtiendo disco",
                        "writing disc" => "Escribiendo PBP",
                        "extracting disc" => "Extrayendo disco",
                        "decompressing file" => "Descomprimiendo",
                        _ => "Procesando"
                    };
                    Append("FASE: " + job.CurrentPhase + " · " + job.Name);
                    continue;
                }
                Append("Motor: " + line);
            }
            return captured.ToString();
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_running) return;
            Append("Solicitada la cancelación al finalizar/cancelar el trabajo activo.");
            _cancel?.Cancel();
            try
            {
                if (_activeProcess != null && !_activeProcess.HasExited)
                    _activeProcess.Kill(entireProcessTree: true);
            }
            catch { /* Process may have exited between checks */ }
        }

        private void SaveLog()
        {
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "LOGS");
                try { Directory.CreateDirectory(dir); }
                catch
                {
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "PSXPackagerLNR", "Logs");
                    Directory.CreateDirectory(dir);
                }
                var path = Path.Combine(dir, "LNR_GUI_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllLines(path, _messages, new UTF8Encoding(true));
                Append("Informe guardado: " + path);
            }
            catch (Exception ex) { Append("AVISO: no se pudo guardar el informe: " + ex.Message); }
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            if (!_running)
            {
                SavePreferences();
                return;
            }
            e.Cancel = true;
            MessageBox.Show(this,
                "Hay una conversión en curso. Pulsa Cancelar y cierra esta ventana cuando termine.",
                "Conversión activa", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public sealed class LnrConversionJob : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private string _status = "Pendiente";
            private int _progressPercent;
            private string _currentPhase = "Iniciando";
            private long? _outputBytes;
            private TimeSpan? _elapsed;

            public LnrConversionJob(string name, List<string> discs, string origin)
            {
                Name = name;
                Discs = discs;
                Origin = origin;
                try
                {
                    SourceBytes = discs.Sum(path => new FileInfo(path).Length);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Do not block all games if one file is not accessible.
                    SourceBytes = null;
                }
            }

            public string Name { get; }
            public List<string> Discs { get; }
            public int DiscCount => Discs.Count;
            public string Origin { get; }
            public long? SourceBytes { get; }
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

            public string SourceSizeText => SourceBytes.HasValue
                ? Size(SourceBytes.Value) : "—";

            public string OutputSizeText => OutputBytes.HasValue
                ? Size(OutputBytes.Value) : "—";

            // This measures PBP minus CHD: positive means PBP takes more space.
            public long? SizeDifference => SourceBytes.HasValue && OutputBytes.HasValue
                ? OutputBytes.Value - SourceBytes.Value : null;

            public string VariationText
            {
                get
                {
                    if (!SizeDifference.HasValue) return "—";
                    var diff = SizeDifference.Value;
                    return (diff > 0 ? "+" : "") + Size(diff);
                }
            }

            public string VariationTooltip
            {
                get
                {
                    if (!SizeDifference.HasValue || !SourceBytes.HasValue || SourceBytes == 0)
                        return "Se calcula al disponer de ambos tamaños.";
                    var diff = SizeDifference.Value;
                    var percent = 100.0 * diff / SourceBytes.Value;
                    var change = diff > 0 ? "El PBP ocupa más que el CHD"
                               : diff < 0 ? "El PBP ocupa menos que el CHD"
                               : "El tamaño es el mismo";
                    return change + ": " + VariationText + " (" +
                           (percent > 0 ? "+" : "") + percent.ToString("N2", Es) + " %).";
                }
            }

            public System.Windows.Media.Brush VariationBrush =>
                SizeDifference > 0 ? System.Windows.Media.Brushes.Firebrick :
                SizeDifference < 0 ? System.Windows.Media.Brushes.ForestGreen :
                System.Windows.Media.Brushes.DimGray;

            public string ElapsedText
            {
                get
                {
                    if (!Elapsed.HasValue) return "—";
                    var elapsed = Elapsed.Value;
                    return elapsed.TotalHours >= 1
                        ? elapsed.ToString(@"hh\:mm\:ss")
                        : elapsed.ToString(@"mm\:ss");
                }
            }

            public void ReadExistingPbp(string path)
            {
                try { OutputBytes = new FileInfo(path).Length; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    OutputBytes = null;
                }
            }

            public string Status
            {
                get => _status;
                set
                {
                    if (_status == value) return;
                    _status = value;
                    Notify(nameof(Status));
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
                    Notify(nameof(ProgressLabel));
                }
            }

            public string CurrentPhase
            {
                get => _currentPhase;
                set
                {
                    if (_currentPhase == value) return;
                    _currentPhase = value;
                    Notify(nameof(CurrentPhase));
                    Notify(nameof(DisplayStatus));
                }
            }

            public string ProgressLabel => ProgressPercent + "%";

            // Only display the phase name here. The percentage is already
            // visible beside its graphical progress bar, avoiding duplication.
            public string DisplayStatus => Status == "Convirtiendo"
                ? CurrentPhase : Status;

            private void Notify(string property)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
            }
        }
    }
}