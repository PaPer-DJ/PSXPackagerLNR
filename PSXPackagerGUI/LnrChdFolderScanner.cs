using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Shared read-only CHD discovery for LNR folder picker and batch planner.
    /// Never follows directory junctions/symlinks, so recursive scans cannot
    /// escape the chosen tree or loop indefinitely. Skips all PBP output trees.
    /// </summary>
    internal static class LnrChdFolderScanner
    {
        public static IReadOnlyList<string> EnumerateFolders(
            string root, bool includeSubfolders, Action<string> warning = null)
        {
            var origin = Path.GetFullPath(root);
            if (!Directory.Exists(origin))
                throw new DirectoryNotFoundException("No existe la carpeta: " + origin);

            var result = new List<string>();
            var stack = new Stack<string>();
            stack.Push(origin);
            while (stack.Count > 0)
            {
                var folder = stack.Pop();
                result.Add(folder);
                if (!includeSubfolders)
                    continue;

                string[] children;
                try
                {
                    children = Directory.GetDirectories(folder);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warning?.Invoke("No se pudieron leer subcarpetas de " + folder + ": " + ex.Message);
                    continue;
                }

                // DFS, with a predictable alphabetic enumeration across runs.
                foreach (var child in children.OrderByDescending(
                             Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(child));
                    if (name.Equals("PBP", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(".tools", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith(".lnr_stage_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                        {
                            warning?.Invoke("Enlace o punto de unión omitido: " + child);
                            continue;
                        }
                        stack.Push(child);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        warning?.Invoke("Subcarpeta omitida: " + child + ". " + ex.Message);
                    }
                }
            }
            return result;
        }

        public static IReadOnlyList<string> FindChds(
            string folder, Action<string> warning = null)
        {
            try
            {
                return Directory.EnumerateFiles(folder, "*.chd", SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warning?.Invoke("No se pudieron leer CHD de " + folder + ": " + ex.Message);
                return Array.Empty<string>();
            }
        }

        public static int CountChds(string root, bool includeSubfolders)
        {
            var count = 0;
            foreach (var folder in EnumerateFolders(root, includeSubfolders))
                count += FindChds(folder).Count;
            return count;
        }

        public static string RelativeFolder(string root, string folder)
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(folder));
            if (relative == ".") return "";
            if (Path.IsPathRooted(relative) || relative == ".." ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Carpeta CHD fuera del origen: " + folder);
            return relative;
        }
    }
}
