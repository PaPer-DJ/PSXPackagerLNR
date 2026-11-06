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

        public LnrBatchWindow()
        {
            InitializeComponent();
            JobList.ItemsSource = _jobs;

            // When installed as a portable toolkit, locate the ROM folder
            // two levels above .tools\LNR_CHD_A_PBP.
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            if (current.Parent?.Name.Equals(".tools", StringComparison.OrdinalIgnoreCase) == true)
            {
                SourceBox.Text = current.Parent.Parent?.FullName ?? current.FullName;
            }
            else
            {
                SourceBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            Append("Selecciona una carpeta con archivos CHD y pulsa Analizar.");
        }

        private void Append(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _messages.Add(entry);
            OutputBox.AppendText(entry + Environment.NewLine);
            if (OutputBox.Text.Length > 160000)
            {
                OutputBox.Text = OutputBox.Text.Substring(OutputBox.Text.Length - 120000);
            }
            OutputBox.ScrollToEnd();
        }

        private static string Size(long bytes) => (bytes / 1048576.0).ToString("N2", Es) + " MB";

        private bool IsMerged => ModeCombo.SelectedIndex == 1;

        private int SelectedLevel => int.Parse(((ComboBoxItem)LevelCombo.SelectedItem).Tag.ToString(),
                                               CultureInfo.InvariantCulture);

        private void Browse_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta de CHD" };
            if (Directory.Exists(SourceBox.Text))
                dialog.InitialDirectory = SourceBox.Text;

            if (dialog.ShowDialog(this) == true)
            {
                SourceBox.Text = dialog.FolderName;
                _jobs.Clear();
                _planFolder = null;
                CountLabel.Text = "Pendiente de analizar";
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
            foreach (var job in Plan(folder, IsMerged))
                _jobs.Add(job);

            _planFolder = folder;
            _planMode = ModeCombo.SelectedIndex;
            CountLabel.Text = _jobs.Count + " PBP previstos";
            SummaryLabel.Text = "Salida: " + Path.Combine(folder, "PBP") + " · originales intactos.";
            Append($"Plan preparado: {_jobs.Count} PBP / modo {(IsMerged ? "UNIDOS" : "SEPARADOS")}, compresión {SelectedLevel}.");
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
                    $"Convertir {_jobs.Count} juegos a {Path.Combine(folder, "PBP")}?\n" +
                    $"Modo: {(IsMerged ? "Unidos" : "Separados")}; compresión: {SelectedLevel}.\n" +
                    "Los CHD originales no se modificarán. Los PBP existentes se omitirán.",
                    "Confirmar conversión LNR", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;

                _running = true;
                _cancel = new CancellationTokenSource();
                SetControls(false);
                _sumOriginal = _sumConverted = 0;
                _succeeded = _skipped = _failed = 0;
                foreach (var job in _jobs) job.Status = "Pendiente";
                await ConvertAllAsync(cli, folder, SelectedLevel, _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                Append("Conversión cancelada a petición del usuario.");
            }
            catch (Exception ex)
            {
                Append("ERROR GENERAL: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Error de conversión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _running = false;
                _activeProcess = null;
                _cancel?.Dispose();
                _cancel = null;
                SetControls(true);
                SaveLog();
            }
        }

        private void SetControls(bool idle)
        {
            RunButton.IsEnabled = idle;
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
                try
                {
                    if (File.Exists(finalPath))
                    {
                        job.Status = "Ya existe";
                        _skipped++;
                        Append("OMITIDO: " + job.Name + ".pbp ya existe.");
                        continue;
                    }

                    Directory.CreateDirectory(stage);
                    var input = job.Discs[0];
                    if (job.Discs.Count > 1)
                    {
                        input = Path.Combine(stage, job.Name + ".m3u");
                        // Absolute paths avoid assuming that CHDs live beside the temporary M3U.
                        File.WriteAllLines(input, job.Discs, new UTF8Encoding(false));
                    }
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
                    var outputTask = process.StandardOutput.ReadToEndAsync(cancel);
                    var errorTask = process.StandardError.ReadToEndAsync(cancel);
                    try
                    {
                        await process.WaitForExitAsync(cancel);
                        var stdout = await outputTask;
                        var stderr = await errorTask;
                        if (process.ExitCode != 0)
                        {
                            throw new InvalidOperationException(
                                $"psxpackager.exe devolvió {process.ExitCode}. " +
                                (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim());
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
                    var sourceBytes = job.Discs.Sum(path => new FileInfo(path).Length);
                    var newBytes = new FileInfo(finalPath).Length;
                    _sumOriginal += sourceBytes;
                    _sumConverted += newBytes;
                    _succeeded++;
                    job.Status = "Completado";
                    Append($"OK: {Size(sourceBytes)} CHD → {Size(newBytes)} PBP; variación {Size(sourceBytes - newBytes)}.");

                    foreach (var disc in job.Discs)
                    {
                        var sbi = Path.ChangeExtension(disc, ".sbi");
                        if (!File.Exists(sbi)) continue;
                        var copy = Path.Combine(dest, Path.GetFileName(sbi));
                        if (!File.Exists(copy)) File.Copy(sbi, copy);
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
                    if (Directory.Exists(stage))
                    {
                        try { Directory.Delete(stage, recursive: true); }
                        catch (Exception ex) { Append("AVISO: no se pudo borrar el temporal: " + ex.Message); }
                    }
                    processed++;
                    WorkProgress.Value = 100.0 * processed / total;
                    SummaryLabel.Text = $"Correctos: {_succeeded} · Omitidos: {_skipped} · Errores: {_failed} " +
                                        $"· {processed}/{total}";
                }
            }

            Append($"FIN. Correctos: {_succeeded}, omitidos: {_skipped}, errores: {_failed}.");
            if (_sumOriginal > 0)
                Append($"Total convertido: {Size(_sumOriginal)} CHD → {Size(_sumConverted)} PBP. " +
                       $"Diferencia: {Size(_sumOriginal - _sumConverted)} " +
                       $"({(100.0 * (_sumOriginal - _sumConverted) / _sumOriginal).ToString("N2", Es)} %).");
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
            if (!_running) return;
            e.Cancel = true;
            MessageBox.Show(this,
                "Hay una conversión en curso. Pulsa Cancelar y cierra esta ventana cuando termine.",
                "Conversión activa", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public sealed class LnrConversionJob : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private string _status = "Pendiente";

            public LnrConversionJob(string name, List<string> discs, string origin)
            {
                Name = name;
                Discs = discs;
                Origin = origin;
            }

            public string Name { get; }
            public List<string> Discs { get; }
            public int DiscCount => Discs.Count;
            public string Origin { get; }
            public string Status
            {
                get => _status;
                set
                {
                    _status = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                }
            }
        }
    }
}