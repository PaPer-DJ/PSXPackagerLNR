using System;
using System.IO;
using System.Linq;
using PSXPackagerGUI;

var root = Path.Combine(Path.GetTempPath(), "LnrDreamcastGdiSmoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("Dreamcast GDI: " + message);
}
static void Reject(Action test, string text)
{
    try { test(); }
    catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Dreamcast unsafe input accepted: " + text);
}
try
{
    var gdi = Path.Combine(root, "Sonic Adventure.gdi");
    var a = Path.Combine(root, "track01.bin");
    var b = Path.Combine(root, "track02.raw");
    var c = Path.Combine(root, "track 03.bin");
    File.WriteAllBytes(a, new byte[2352 * 20]);
    File.WriteAllBytes(b, new byte[2352 * 20]);
    File.WriteAllBytes(c, new byte[2048 * 60]);
    File.WriteAllText(gdi,
        "3\n" +
        "1 0 4 2352 track01.bin 0\n" +
        "2 450 0 2352 track02.raw 0\n" +
        "3 45000 4 2048 \"track 03.bin\" 0\n");
    var result = LnrDreamcastGdi.Validate(gdi);
    Assert(result.Tracks.Count == 3, "must find three ordered tracks");
    Assert(result.Tracks[2].Number == 3 && result.Tracks[2].Lba == 45000,
           "high-density track metadata");
    Assert(result.InputBytes == File.ReadAllBytes(gdi).Length +
        new FileInfo(a).Length + new FileInfo(b).Length + new FileInfo(c).Length,
        "input size must include all track data");
    Assert(LnrDreamcastGdi.IsGdromMetadata("Metadata: Tag='CHGD' Index=0"),
           "GD-ROM metadata not recognized");
    Assert(LnrDreamcastGdi.IsGdromMetadata("Metadata: Tag='CHGT' Index=0"),
           "legacy GD-ROM metadata not recognized");
    Assert(!LnrDreamcastGdi.IsGdromMetadata("Tag='CHT2' | Tag='CHCD'"),
           "CD-ROM must not be accepted as Dreamcast");
    Assert(!LnrDreamcastGdi.IsGdromMetadata(""), "empty metadata must fail");

    File.WriteAllText(gdi,
        "3\n" +
        "1 0 4 2352 track01.bin 0\n" +
        "2 450 0 2352 track02.raw 0\n" +
        "3 45000 4 2048 track 03.bin 0\n");
    Assert(LnrDreamcastGdi.Validate(gdi).Tracks.Count == 3,
           "unquoted track filename containing spaces should work");

    File.WriteAllText(gdi, "3\n1 0 4 2352 track01.bin 0\n2 450 0 2352 track02.raw 0\n");
    Reject(() => LnrDreamcastGdi.Validate(gdi), "descriptor missing a track");
    File.WriteAllText(gdi,
        "3\n1 0 4 2352 track01.bin 0\n2 450 0 2352 track02.raw 0\n" +
        "2 45000 4 2048 \"track 03.bin\" 0\n");
    Reject(() => LnrDreamcastGdi.Validate(gdi), "duplicated track number");
    File.WriteAllText(gdi,
        "3\n1 0 4 2352 track01.bin 0\n2 450 0 2352 track02.raw 0\n" +
        "3 45000 4 2048 missing.bin 0\n");
    Reject(() => LnrDreamcastGdi.Validate(gdi), "missing BIN");
    File.WriteAllText(gdi,
        "3\n1 0 4 2352 track01.bin 0\n2 450 0 2352 track02.raw 0\n" +
        "3 45000 4 2048 ../outside.bin 0\n");
    Reject(() => LnrDreamcastGdi.Validate(gdi), "path traversal");
    File.WriteAllText(gdi,
        "3\n1 0 4 2352 track01.bin 0\n2 450 0 2352 track02.raw 0\n" +
        "3 45000 4 2048 \"track 03.bin\" 4\n");
    Reject(() => LnrDreamcastGdi.Validate(gdi), "misaligned data after offset");
    File.WriteAllText(gdi,
        "3\n1 0 4 2352 track01.bin 0\n2 450 0 2352 track02.raw 0\n" +
        "3 460 4 2048 \"track 03.bin\" 0\n");
    Reject(() => LnrDreamcastGdi.Validate(gdi), "missing GD-ROM high density");
    Console.WriteLine("Dreamcast GDI smoke: valid 3-track GDI, spaces, CHGD/CHGT metadata, " +
        "reject CD-ROM, missing tracks, duplicates, traversal, misaligned sectors and non-GD-ROM.");
}
finally
{
    Directory.Delete(root, recursive: true);
}
