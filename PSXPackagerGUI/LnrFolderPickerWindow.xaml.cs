using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Folder selection with a CHD preview. Unlike OpenFolderDialog, displays
    /// actual games before the user commits to selecting a folder.
    /// </summary>
    public partial class LnrFolderPickerWindow : Window
    {
        private string _currentFolder = "";
        private int _chdCount;
        private List<GamePreview> _directChds = new();

        public string SelectedFolder { get; private set; } = "";
        public bool SelectedIncludeSubfolders { get; private set; }

        public LnrFolderPickerWindow(string initialFolder, bool includeSubfolders = false)
        {
            InitializeComponent();
            IncludeSubfoldersCheck.IsChecked = includeSubfolders;
            var start = Directory.Exists(initialFolder)
                ? initialFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Navigate(start);
        }

        private void Navigate(string path)
        {
            try
            {
                var folder = Path.GetFullPath(path.Trim().Trim('"'));
                if (!Directory.Exists(folder))
                    throw new DirectoryNotFoundException("La carpeta no existe: " + folder);

                var folders = new List<FolderPreview>();
                foreach (var subdir in Directory.EnumerateDirectories(folder))
                {
                    try
                    {
                        folders.Add(new FolderPreview(subdir));
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                folders = folders.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

                var chds = new List<GamePreview>();
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    if (!Path.GetExtension(file).Equals(".chd", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        chds.Add(new GamePreview(file));
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                chds = chds.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

                _currentFolder = folder;
                _chdCount = chds.Count;
                PathBox.Text = folder;
                FolderList.ItemsSource = folders;
                _directChds = chds;
                UpButton.IsEnabled = Directory.GetParent(folder) != null;
                UpdateScope();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
            {
                StatusLabel.Text = "No se puede acceder a esa carpeta: " + ex.Message;
            }
        }

        private void UpdateScope()
        {
            if (StatusLabel == null || !Directory.Exists(_currentFolder)) return;
            var recursive = IncludeSubfoldersCheck.IsChecked == true;
            var count = _chdCount;
            var previewTruncated = false;
            try
            {
                if (recursive)
                {
                    const int previewLimit = 1500;
                    var previews = new List<GamePreview>();
                    count = 0;
                    foreach (var subfolder in LnrChdFolderScanner.EnumerateFolders(_currentFolder, true))
                    {
                        foreach (var path in LnrChdFolderScanner.FindChds(subfolder))
                        {
                            count++;
                            if (previews.Count >= previewLimit)
                            {
                                previewTruncated = true;
                                continue;
                            }
                            try
                            {
                                previews.Add(new GamePreview(path, Path.GetRelativePath(_currentFolder, path)));
                            }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                        }
                    }
                    GameList.ItemsSource = previews
                        .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }
                else
                {
                    GameList.ItemsSource = _directChds;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusLabel.Text = "No se pudieron analizar todas las subcarpetas: " + ex.Message;
                SelectButton.IsEnabled = false;
                return;
            }
            GamesHeading.Text = recursive
                ? "Juegos CHD de esta carpeta y subcarpetas"
                : "Juegos CHD de esta carpeta";
            SelectButton.IsEnabled = count > 0;
            ScopeDescription.Text = recursive
                ? "Se analizarán los CHD de esta carpeta y sus subcarpetas."
                : "Se analizarán todos los archivos CHD de esta carpeta, sin entrar en subcarpetas.";
            StatusLabel.Text = count == 0
                ? (recursive
                   ? "No hay CHD en esta carpeta ni en sus subcarpetas accesibles."
                   : "No hay CHD directamente aquí. Entra en otra carpeta o marca «También subcarpetas».")
                : recursive
                    ? $"{count} CHD en esta carpeta y subcarpetas (sin PBP ni enlaces). Pulsa «Usar esta carpeta»." +
                      (previewTruncated ? " La lista muestra solo los primeros 1.500." : "")
                    : $"{count} juego(s) CHD disponibles en esta carpeta. Pulsa «Usar esta carpeta» para continuar.";
        }

        private void IncludeSubfolders_OnChanged(object sender, RoutedEventArgs e)
        {
            UpdateScope();
        }

        private void Go_OnClick(object sender, RoutedEventArgs e) => Navigate(PathBox.Text);

        private void PathBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Navigate(PathBox.Text);
                e.Handled = true;
            }
        }

        private void Up_OnClick(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(_currentFolder);
            if (parent != null) Navigate(parent.FullName);
        }

        private void FolderList_OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FolderList.SelectedItem is FolderPreview folder)
                Navigate(folder.FullPath);
        }

        private void NativeBrowse_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Busca una carpeta con juegos CHD (después podrás ver la lista)",
                InitialDirectory = _currentFolder
            };
            if (dialog.ShowDialog(this) == true) Navigate(dialog.FolderName);
        }

        private void Select_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectButton.IsEnabled != true || !Directory.Exists(_currentFolder))
            {
                StatusLabel.Text = "Selecciona primero una carpeta que contenga archivos CHD.";
                return;
            }
            SelectedFolder = _currentFolder;
            SelectedIncludeSubfolders = IncludeSubfoldersCheck.IsChecked == true;
            DialogResult = true;
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

        public sealed class FolderPreview
        {
            public FolderPreview(string path)
            {
                FullPath = path;
                Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            }
            public string FullPath { get; }
            public string Name { get; }
        }

        public sealed class GamePreview
        {
            public GamePreview(string path, string displayName = null)
            {
                var file = new FileInfo(path);
                Name = displayName ?? file.Name;
                Size = (file.Length / 1048576.0).ToString("N1",
                    CultureInfo.GetCultureInfo("es-ES")) + " MB";
            }
            public string Name { get; }
            public string Size { get; }
        }
    }
}