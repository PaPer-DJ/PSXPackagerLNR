using PSXPackagerGUI;
using System;
using System.IO;
using System.Linq;

var root = Path.Combine(Path.GetTempPath(), "LnrFolderScanSmoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    void Chd(string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
    }
    void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    Chd("Juego.chd");
    Chd(Path.Combine("Carpeta 1", "Juego.chd"));
    Chd(Path.Combine("Carpeta 1", "CD Sub", "Disco.chd"));
    Chd(Path.Combine("Carpeta 2", "Juego.chd"));
    Chd(Path.Combine("PBP", "NoConvertir.chd"));
    Chd(Path.Combine("Carpeta 1", "PBP", "NoConvertir2.chd"));
    Chd(Path.Combine(".tools", "NoConvertir3.chd"));
    Chd(Path.Combine("Carpeta 2", ".lnr_stage_abcd", "NoConvertir4.chd"));

    var flat = LnrChdFolderScanner.EnumerateFolders(root, false);
    Assert(flat.Count == 1 && flat[0] == root, "Flat mode must not enter subfolders");
    Assert(LnrChdFolderScanner.CountChds(root, false) == 1, "Flat CHD count");
    var folders = LnrChdFolderScanner.EnumerateFolders(root, true);
    Assert(folders.Count == 4, "Only root and 3 real source directories should be scanned");
    Assert(LnrChdFolderScanner.CountChds(root, true) == 4, "Recursive CHD count must exclude output and staging");
    var names = folders.SelectMany(f => LnrChdFolderScanner.FindChds(f).Select(p =>
         Path.Combine(LnrChdFolderScanner.RelativeFolder(root, f), Path.GetFileName(p))))
         .ToArray();
    Assert(names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4,
        "Relative output paths allow duplicate filenames across subfolders");
    Assert(names.Count(x => Path.GetFileName(x).Equals("Juego.chd", StringComparison.OrdinalIgnoreCase)) == 3,
        "Three identically named source games must remain distinct");

    foreach(var folder in folders)
    {
        var rel = LnrChdFolderScanner.RelativeFolder(root, folder);
        Assert(!Path.IsPathRooted(rel), "Folder relative name must not be absolute");
        Assert(!rel.StartsWith("..", StringComparison.Ordinal), "No relative traversal");
    }
    var outside = Path.Combine(Path.GetDirectoryName(root)!, "outside_" + Guid.NewGuid().ToString("N"));
    var rejected = false;
    try { _ = LnrChdFolderScanner.RelativeFolder(root, outside); }
    catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "A directory outside the root must be rejected");

    Console.WriteLine("LNR recursive folder scan: flat, nested, duplicate basenames, PBP/.tools/staging exclusions, traversal validation passed.");
}
finally
{
    Directory.Delete(root, recursive: true);
}
