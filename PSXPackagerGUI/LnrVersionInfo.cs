using System;
using System.IO;

namespace PSXPackagerGUI
{
    /// <summary>
    /// Version of the LNR distribution (separate from original PSXPackager version).
    /// CI bundles LNR-VERSION.txt next to the Windows executable.
    /// </summary>
    public static class LnrVersionInfo
    {
        public static string Display
        {
            get
            {
                try
                {
                    var file = Path.Combine(AppContext.BaseDirectory, "LNR-VERSION.txt");
                    if (File.Exists(file))
                    {
                        var version = File.ReadAllText(file).Trim();
                        if (!string.IsNullOrWhiteSpace(version)) return version;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                return "versión de desarrollo (sin etiqueta)";
            }
        }
    }
}
