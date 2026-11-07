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

        public string SelectedFolder { get; private set; } = "";

        public LnrFolderPickerWindow(string initialFolder)
        {
            InitializeComponent();
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
                GameList.ItemsSource = chds;
                UpButton.IsEnabled = Directory.GetParent(folder) != null;
                SelectButton.IsEnabled = _chdCount > 0;
                StatusLabel.Text = _chdCount == 0
                    ? "No hay archivos CHD directamente en esta carpeta. Entra en una subcarpeta o escribe otra ruta."
                    : $"{_chdCount} juego(s) CHD disponibles en esta carpeta. Pulsa «Usar esta carpeta» para continuar.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
            {
                StatusLabel.Text = "No se puede acceder a esa carpeta: " + ex.Message;
            }
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
            if (_chdCount == 0 || !Directory.Exists(_currentFolder))
            {
                StatusLabel.Text = "Selecciona primero una carpeta que contenga archivos CHD.";
                return;
            }
            SelectedFolder = _currentFolder;
            DialogResult = true;
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

        private sealed class FolderPreview
        {
            public FolderPreview(string path)
            {
                FullPath = path;
                Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            }
            public string FullPath { get; }
            public string Name { get; }
        }

        private sealed class GamePreview
        {
            public GamePreview(string path)
            {
                var file = new FileInfo(path);
                Name = file.Name;
                Size = (file.Length / 1048576.0).ToString("N1",
                    CultureInfo.GetCultureInfo("es-ES")) + " MB";
            }
            public string Name { get; }
            public string Size { get; }
        }
    }
}