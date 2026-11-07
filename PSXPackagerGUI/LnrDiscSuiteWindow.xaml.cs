using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private CancellationTokenSource _cancel;
        private Process _activeProcess;
        private bool _running;
        private readonly CultureInfo _es = CultureInfo.GetCultureInfo("es-ES");

        public LnrDiscSuiteWindow()
        {
            InitializeComponent();
            Title = "PSXPackagerLNR · " + LnrVersionInfo.Display + " | Suite de discos";
            JobsList.ItemsSource = _jobs;
            ModeBox.SelectedIndex = 0;
            var prefs = LnrGuiPreferences.Load();
            SourceBox.Text = Directory.Exists(prefs.LastSourceFolder)
                ? prefs.LastSourceFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var localChdman = Path.Combine(AppContext.BaseDirectory, "chdman.exe");
            ChdmanBox.Text = File.Exists(localChdman) ? localChdman : "";
            Log("Este módulo utiliza CHDMAN de MAME. No cambia ni elimina imágenes originales.");
            Log("Los PBP multidisco se extraerán y convertirán a un CHD independiente por disco.");
            Scan();
        }

        private int Mode => ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
        private string SourceExtension => Mode == 0 ? ".pbp" : ".chd";
        private string OutputSubdirectory => Mode switch
        {
            0 => "CHD",
            1 => "BIN_CUE",
            _ => "ISO"
        };

        private void Log(string message)
        {
            foreach (var line in (message ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                LogBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                                  line.TrimEnd() + Environment.NewLine);
            }
            if (LogBox.Text.Length > 120000)
                LogBox.Text = LogBox.Text.Substring(LogBox.Text.Length - 80000);
            LogBox.ScrollToEnd();
        }

        private void Mode_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !_running) Scan();
        }

        private void Source_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _running) return;
            Scan();
            e.Handled = true;
        }

        private void Browse_OnClick(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFolderDialog
            {
                Title = "Elige la carpeta con los archivos " + SourceExtension,
                InitialDirectory = Directory.Exists(SourceBox.Text)
                    ? SourceBox.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (picker.ShowDialog(this) != true) return;
            SourceBox.Text = picker.FolderName;
            Scan();
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
            new LnrHelpWindow { Owner = this }.ShowDialog();
        }

        private void Scan()
        {
            if (_running) return;
            _jobs.Clear();
            BatchProgress.Value = 0;
            ProgressLabel.Text = "0/0 · sin iniciar";
            try
            {
                var folder = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
                if (!Directory.Exists(folder))
                {
                    CountLabel.Text = "Carpeta no encontrada";
                    ConvertButton.IsEnabled = false;
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(folder)
                         .Where(f => string.Equals(Path.GetExtension(f),
                             SourceExtension, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(file => Path.GetFileName(file), StringComparer.CurrentCultureIgnoreCase))
                {
                    var size = new FileInfo(file).Length;
                    _jobs.Add(new DiscJob(file, (size / 1048576.0).ToString("N2", _es) + " MB"));
                }

                CountLabel.Text = _jobs.Count + " archivos";
                ConvertButton.IsEnabled = _jobs.Count > 0;
                OutputLabel.Text = "Salida: " + Path.Combine(folder, OutputSubdirectory) +
                                   " · todos los originales se conservarán.";
                Log($"Analizados {_jobs.Count} archivos {SourceExtension} para {ModeBox.Text}.");
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
            FindChdmanButton.IsEnabled = !running;
            AnalyzeButton.IsEnabled = !running;
            ModeBox.IsEnabled = !running;
            SourceBox.IsEnabled = !running;
            ChdmanBox.IsEnabled = !running;
            ConvertButton.IsEnabled = !running && _jobs.Count > 0;
            CancelButton.IsEnabled = running;
        }

        private async void Convert_OnClick(object sender, RoutedEventArgs e)
        {
            if (_running) return;
            Scan();
            if (_jobs.Count == 0) return;

            var chdman = ChdmanBox.Text.Trim().Trim('"');
            if (!File.Exists(chdman) ||
                !string.Equals(Path.GetFileName(chdman), "chdman.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Este módulo necesita chdman.exe de la distribución oficial de MAME.\n\n" +
                    "Colócalo junto a PSXPackagerGUI.exe o usa «Localizar...».\n" +
                    "Puedes obtener MAME y consultar la herramienta en https://www.mamedev.org/release.html\n\n" +
                    "El conversor directo CHD → PBP continúa funcionando SIN CHDMAN.",
                    "Falta CHDMAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var folder = Path.GetFullPath(SourceBox.Text.Trim().Trim('"'));
            var destination = Path.Combine(folder, OutputSubdirectory);
            var message = $"Modo: {ModeBox.Text}\n" +
                          $"Archivos de origen: {_jobs.Count}\n" +
                          $"Destino: {destination}\n\n" +
                          "No se eliminará ningún original.\n" +
                          "Los resultados existentes se respetarán.\n\n" +
                          (Mode == 0
                              ? "PBP → CHD utiliza BIN/CUE intermedios temporales, que se eliminan al terminar cada juego."
                              : "Elige el tipo correcto de disco: CD (BIN/CUE) o DVD (ISO).") +
                          "\n\n¿Iniciar el lote?";
            if (MessageBox.Show(this, message, "Confirmar lote de la suite",
                                MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
                return;

            _cancel = new CancellationTokenSource();
            SetRunning(true);
            int success = 0, skipped = 0, errors = 0, processed = 0;
            try
            {
                Directory.CreateDirectory(destination);
                foreach (var job in _jobs)
                {
                    _cancel.Token.ThrowIfCancellationRequested();
                    try
                    {
                        job.Status = "Procesando";
                        var made = Mode switch
                        {
                            0 => await PbpToChdAsync(job, destination, chdman, _cancel.Token),
                            1 => await ChdToCueAsync(job, destination, chdman, _cancel.Token),
                            _ => await ChdToIsoAsync(job, destination, chdman, _cancel.Token)
                        };
                        job.OutputCount = made.ToString(CultureInfo.InvariantCulture);
                        if (made == 0)
                        {
                            skipped++;
                            job.Status = "Saltado / Ya existe";
                        }
                        else
                        {
                            success++;
                            job.Status = "Completado";
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        job.Status = "Cancelado";
                        throw;
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        job.Status = "ERROR";
                        Log("ERROR " + job.Name + ": " + ex.Message);
                    }
                    finally
                    {
                        processed++;
                        BatchProgress.Value = 100.0 * processed / _jobs.Count;
                        ProgressLabel.Text = $"{processed}/{_jobs.Count} · Correctos: {success} · " +
                                             $"Saltados: {skipped} · Errores: {errors}";
                    }
                }
                Log($"FIN. Correctos: {success}; saltados: {skipped}; errores: {errors}.");
            }
            catch (OperationCanceledException)
            {
                Log("Lote cancelado. Originales conservados; salidas anteriores al cancelado se mantienen.");
            }
            catch (Exception ex)
            {
                Log("ERROR GENERAL: " + ex.Message);
            }
            finally
            {
                SetRunning(false);
                _cancel?.Dispose();
                _cancel = null;
            }
        }

        private async Task<int> PbpToChdAsync(DiscJob job, string dest,
                                             string chdman, CancellationToken token)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "psxpackager.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException("Falta psxpackager.exe junto a la GUI.", exe);

            var stage = NewStageFolder(dest);
            var converted = 0;
            try
            {
                Log("Extrayendo PBP: " + job.Name);
                await ExecuteAsync(exe, new[] { "-i", job.Source, "-o", stage,
                                                 "-s", "-f", "%FILENAME%" }, token);
                var cues = Directory.EnumerateFiles(stage, "*.cue", SearchOption.TopDirectoryOnly)
                                    .OrderBy(file => Path.GetFileName(file), StringComparer.CurrentCultureIgnoreCase).ToList();
                if (cues.Count == 0)
                    throw new InvalidDataException("El PBP no proporcionó ningún archivo CUE. " +
                                                   "Comprueba que contiene discos PS1 compatibles.");
                foreach (var cue in cues)
                {
                    token.ThrowIfCancellationRequested();
                    var name = Path.GetFileNameWithoutExtension(cue);
                    var output = Path.Combine(dest, name + ".chd");
                    if (File.Exists(output))
                    {
                        Log("SALTADO: " + Path.GetFileName(output) + " ya existe.");
                        continue;
                    }

                    var tempChd = Path.Combine(stage, name + ".chd");
                    Log("Generando CHD desde " + Path.GetFileName(cue));
                    await ExecuteAsync(chdman, new[] { "createcd", "-i", cue, "-o", tempChd }, token);
                    VerifyChdHeader(tempChd);
                    await ExecuteAsync(chdman, new[] { "verify", "-i", tempChd }, token);
                    if (File.Exists(output))
                    {
                        Log("SALTADO: destino apareció durante la conversión: " + Path.GetFileName(output));
                        continue;
                    }
                    File.Move(tempChd, output);
                    VerifyChdHeader(output);
                    converted++;
                    Log("OK: " + Path.GetFileName(output));
                }
                return converted;
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
                                                    "-o", cue, "-ob", bin }, token);
                if (!File.Exists(cue) || new FileInfo(cue).Length < 24 ||
                    !File.Exists(bin) || new FileInfo(bin).Length < 4096)
                    throw new InvalidDataException("La extracción no generó BIN y CUE válidos.");
                if (!File.ReadAllText(cue).Contains(Path.GetFileName(bin),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El CUE no referencia al BIN esperado.");
                if (Directory.Exists(final))
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
                                                    "-o", iso }, token);
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

        private async Task ExecuteAsync(string exe, IEnumerable<string> arguments,
                                        CancellationToken token)
        {
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
            _activeProcess = proc;
            try
            {
                if (!proc.Start())
                    throw new InvalidOperationException("No se pudo iniciar: " + Path.GetFileName(exe));
                var stdout = proc.StandardOutput.ReadToEndAsync(token);
                var stderr = proc.StandardError.ReadToEndAsync(token);
                await proc.WaitForExitAsync(token);
                var a = await stdout;
                var b = await stderr;
                if (proc.ExitCode != 0)
                {
                    var detail = string.IsNullOrWhiteSpace(b) ? a : b;
                    detail = detail.Length > 900 ? detail.Substring(detail.Length - 900) : detail;
                    throw new InvalidOperationException(Path.GetFileName(exe) + " devolvió " +
                                                        proc.ExitCode + ": " + detail.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                if (!proc.HasExited) proc.Kill(entireProcessTree: true);
                throw;
            }
            finally
            {
                _activeProcess = null;
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            _cancel?.Cancel();
            CancelButton.IsEnabled = false;
            Log("Cancelación solicitada…");
            try
            {
                if (_activeProcess is { HasExited: false })
                    _activeProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            if (!_running) return;
            e.Cancel = true;
            MessageBox.Show(this,
                "Hay una conversión en curso. Pulsa Cancelar y cierra esta ventana al finalizar.",
                "Conversión activa", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public sealed class DiscJob : INotifyPropertyChanged
        {
            private string _status = "Pendiente";
            private string _outputCount = "—";
            public event PropertyChangedEventHandler PropertyChanged;
            public DiscJob(string source, string sizeText)
            {
                Source = source;
                SizeText = sizeText;
                Name = Path.GetFileName(source);
            }
            public string Source { get; }
            public string Name { get; }
            public string SizeText { get; }
            public string Status
            {
                get => _status;
                set
                {
                    _status = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                }
            }
            public string OutputCount
            {
                get => _outputCount;
                set
                {
                    _outputCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputCount)));
                }
            }
        }
    }
}