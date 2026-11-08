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
        // Countdown is local to this window; Windows gets no scheduled command.
        private readonly DispatcherTimer _shutdownCountdown = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        private bool _shutdownPending;
        private int _shutdownSecondsRemaining;
        private CancellationTokenSource _cancel;
        private Process _activeProcess;
        private bool _running;
        private readonly CultureInfo _es = CultureInfo.GetCultureInfo("es-ES");

        public LnrDiscSuiteWindow()
        {
            InitializeComponent();
            Title = "PSXPackagerLNR · " + LnrVersionInfo.Display + " | Suite de discos";
            JobsList.ItemsSource = _jobs;
            TrackManualColumnResizing();
            _shutdownCountdown.Tick += ShutdownCountdown_OnTick;
            ShutdownCheck.IsChecked = false; // Never restored from preferences.
            ModeBox.SelectedIndex = 0;
            var prefs = LnrGuiPreferences.Load();
            SourceBox.Text = Directory.Exists(prefs.LastSourceFolder)
                ? prefs.LastSourceFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
        private string SourceExtension => Mode <= 2 ? ".pbp" : ".chd";
        private bool NeedsChdman => Mode == 0 || Mode >= 3;
        private string OutputSubdirectory => Mode switch
        {
            0 => "CHD",
            1 => "BIN_CUE",
            2 => "ISO",
            3 => "BIN_CUE",
            _ => "ISO"
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
            if (JobsList == null || JobsList.ActualWidth < 420 ||
                JobsList.View is not GridView view || view.Columns.Count != 4 ||
                _settingColumnWidths)
                return;

            double TextWidth(string text, bool heading = false)
            {
                var label = new TextBlock
                {
                    Text = text ?? "—",
                    FontFamily = JobsList.FontFamily,
                    FontSize = JobsList.FontSize,
                    FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return label.DesiredSize.Width;
            }
            double Measured(string heading, Func<DiscJob, string> content, double minimum)
            {
                var longest = TextWidth(heading, true);
                foreach (var job in _jobs)
                    longest = Math.Max(longest, TextWidth(content(job)));
                return Math.Max(minimum, Math.Ceiling(longest + 24));
            }

            var ideal = new double[4];
            ideal[1] = Measured("Tamaño", j => j.SizeText, 95);
            ideal[2] = Math.Max(165, Measured("Estado", j => j.Status, 0));
            // Include the longest status even before any job reaches that state.
            ideal[2] = Math.Max(ideal[2], Math.Ceiling(TextWidth("Saltado / Ya existe") + 26));
            ideal[3] = Measured("Archivos producidos", j => j.OutputCount, 138);

            for (var index = 1; index < ideal.Length; index++)
                if (_manuallySizedColumns.Contains(index))
                    ideal[index] = view.Columns[index].Width;

            var available = JobsList.ActualWidth - SystemParameters.VerticalScrollBarWidth - 8;
            var longestName = Measured("Archivo origen", j => j.Name, 180);
            ideal[0] = _manuallySizedColumns.Contains(0)
                ? view.Columns[0].Width
                : Math.Max(longestName, Math.Floor(available - ideal.Skip(1).Sum()));

            _settingColumnWidths = true;
            try
            {
                for (var index = 0; index < ideal.Length; index++)
                {
                    if (_manuallySizedColumns.Contains(index)) continue;
                    if (Math.Abs(view.Columns[index].Width - ideal[index]) > 0.5)
                        view.Columns[index].Width = ideal[index];
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
            }
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
            new LnrHelpWindow { Owner = this }.ShowDialog();
        }

        private void Scan()
        {
            if (_running) return;
            if (_shutdownPending)
                CancelShutdownCountdown("Apagado automático cancelado al iniciar otro análisis.");
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
            SourceBox.IsEnabled = !running;
            ChdmanBox.IsEnabled = !running && NeedsChdman;
            ConvertButton.IsEnabled = !running && _jobs.Count > 0;
            CancelButton.IsEnabled = running;
            ShutdownCheck.IsEnabled = !running && !_shutdownPending;
        }

        private async void Convert_OnClick(object sender, RoutedEventArgs e)
        {
            if (_running) return;
            Scan();
            if (_jobs.Count == 0) return;

            var chdman = ChdmanBox.Text.Trim().Trim('"');
            if (NeedsChdman && (!File.Exists(chdman) ||
                !string.Equals(Path.GetFileName(chdman), "chdman.exe",
                    StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this,
                    "Este módulo necesita chdman.exe de la distribución oficial de MAME.\n\n" +
                    "Usa «Importar ZIP...», colócalo junto a la GUI o pulsa «Localizar...».\n" +
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
                              ? "PBP → CHD utiliza BIN/CUE intermedios temporales para comprimir cada disco."
                              : Mode == 1
                              ? "PBP → BIN/CUE conserva las pistas de CD disponibles en el PBP, incluso multidisco."
                              : Mode == 2
                              ? "AVISO: ISO solo admite un CD de datos sin audio, sin XA Form 2 ni multidisco.\n" +
                                "Un ISO NO sirve para reconstruir o grabar un CD completo de PS1.\n" +
                                "Los juegos no compatibles darán error sin generar ISO: usa BIN/CUE."
                              : "Elige el tipo correcto de disco: CD (BIN/CUE) o DVD (ISO).") +
                          "\n\n¿Iniciar el lote?";
            if (MessageBox.Show(this, message, "Confirmar lote de la suite",
                                MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
                return;

            var shutdownRequested = ShutdownCheck.IsChecked == true;
            if (shutdownRequested)
            {
                var response = MessageBox.Show(this,
                    "APAGAR EL EQUIPO DESPUÉS DEL LOTE\n\n" +
                    "Se solicitará únicamente si se convierte al menos un archivo, " +
                    "sin errores ni cancelaciones.\n\n" +
                    "Al acabar tendrás una cuenta atrás de 60 segundos para cancelar " +
                    "desde esta ventana. Si la cierras antes, el apagado se cancela.\n\n" +
                    "Windows intentará un apagado normal, sin forzar el cierre de otras " +
                    "aplicaciones. Guarda cualquier documento importante.\n\n" +
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
            _cancel = new CancellationTokenSource();
            SetRunning(true);
            int success = 0, skipped = 0, errors = 0, processed = 0;
            bool completedNormally = false;
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
                            1 => await PbpToCueAsync(job, destination, _cancel.Token),
                            2 => await PbpToIsoAsync(job, destination, _cancel.Token),
                            3 => await ChdToCueAsync(job, destination, chdman, _cancel.Token),
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
                        ResizeSuiteColumns();
                    }
                }
                completedNormally = !_cancelRequested;
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
                ShutdownCheck.IsChecked = false; // System control never persists.
                SaveLog();
                if (shutdownRequested)
                {
                    if (completedNormally && !_cancelRequested && errors == 0 && success > 0)
                        BeginShutdownCountdown();
                    else
                    {
                        Log("APAGADO OMITIDO: lote cancelado, con errores o sin conversiones nuevas.");
                        SaveLog();
                    }
                }
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
            if (Directory.Exists(finalFolder))
            {
                Log("SALTADO: existe carpeta BIN/CUE: " + name);
                return 0;
            }

            var stage = NewStageFolder(dest);
            try
            {
                Log("Extrayendo los discos y pistas de " + job.Name + " a BIN/CUE.");
                await ExecuteAsync(cli, new[]
                {
                    "-i", job.Source, "-o", stage, "-s", "-f", "%FILENAME%"
                }, token);

                var cues = Directory.EnumerateFiles(stage, "*.cue", SearchOption.TopDirectoryOnly).ToArray();
                var bins = Directory.EnumerateFiles(stage, "*.bin", SearchOption.TopDirectoryOnly).ToArray();
                if (cues.Length == 0 || bins.Length == 0)
                    throw new InvalidDataException(
                        "El PBP no generó BIN/CUE completos; no se publicará la salida.");

                foreach (var cue in cues)
                {
                    var cueText = File.ReadAllText(cue);
                    var referenced = System.Text.RegularExpressions.Regex.Matches(
                        cueText, "^\\s*FILE\\s+\\\"(?<name>[^\\\"]+)\\\"\\s+BINARY",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                    if (referenced.Count == 0)
                        throw new InvalidDataException(
                            "Falta el nombre del BIN asociado a " + Path.GetFileName(cue));

                    foreach (System.Text.RegularExpressions.Match match in referenced)
                    {
                        var binName = match.Groups["name"].Value;
                        if (binName != Path.GetFileName(binName) ||
                            !binName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
                            !File.Exists(Path.Combine(stage, binName)) ||
                            new FileInfo(Path.Combine(stage, binName)).Length < 2048)
                            throw new InvalidDataException(
                                "El CUE referencia un BIN ausente o inseguro: " + binName);
                    }
                }

                if (Directory.Exists(finalFolder))
                {
                    Log("SALTADO: la carpeta apareció durante la extracción: " + name);
                    return 0;
                }
                token.ThrowIfCancellationRequested();
                Directory.Move(stage, finalFolder);
                Log($"OK: {cues.Length} disco(s) extraídos en BIN/CUE: " + name);
                return cues.Length;
            }
            finally
            {
                CleanStage(stage);
            }
        }

        /// <summary>
        /// Not a full-disc reconstruction: exports only 2048-byte user data
        /// from single-track Mode1/Mode2 Form1 CDs. No CD-Audio, XA Form2
        /// or multisession/multidisc content is silently discarded.
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

            var stage = NewStageFolder(dest);
            try
            {
                Log("Comprobando si el PBP contiene un único disco y pista de datos: " + job.Name);
                await ExecuteAsync(cli, new[]
                {
                    "-i", job.Source, "-o", stage, "-s", "-f", "%FILENAME%"
                }, token);

                var cues = Directory.EnumerateFiles(stage, "*.cue", SearchOption.TopDirectoryOnly).ToArray();
                if (cues.Length != 1)
                    throw new InvalidDataException(
                        "El PBP contiene varios discos o no generó un CUE válido. " +
                        "ISO no conserva multidisco: utiliza PBP → BIN/CUE.");

                var provisional = Path.Combine(stage, name + ".iso");
                var sectors = await LnrDataTrackIsoExporter.ExportAsync(
                    cues[0], provisional, token);
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
            _cancelRequested = true;
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