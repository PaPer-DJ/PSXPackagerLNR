using PSXPackagerGUI;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

var root = Path.Combine(Path.GetTempPath(), "LNR-Iso-Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    await TestMode1Async();
    await TestMode2Form1Async();
    await ExpectRejectionAsync("AUDIO", 1, 0);
    await ExpectRejectionAsync("MODE2/2352", 2, 0x20);
    await TestMultipleTracksRejectedAsync();
    // Shutdown must never be triggered by incomplete, cancelled or
    // failed batches, nor after a batch in which every PBP was skipped.
    if (!LnrShutdownPolicy.MayShutdown(true, true, false, 0, 0, 3))
        throw new Exception("Completed batch should allow opted-in shutdown.");
    if (LnrShutdownPolicy.MayShutdown(false, true, false, 0, 0, 3) ||
        LnrShutdownPolicy.MayShutdown(true, false, false, 0, 0, 3) ||
        LnrShutdownPolicy.MayShutdown(true, true, true, 0, 0, 3) ||
        LnrShutdownPolicy.MayShutdown(true, true, false, 1, 0, 3) ||
        LnrShutdownPolicy.MayShutdown(true, true, false, 0, 1, 3) ||
        LnrShutdownPolicy.MayShutdown(true, true, false, 0, 0, 0))
        throw new Exception("Unsafe automatic shutdown gate detected.");
    Console.WriteLine("LNR ISO smoke tests: 5 checks passed.");
    Console.WriteLine("LNR shutdown policy: 7 safety conditions passed.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

async Task TestMode1Async()
{
    var files = MakeDisc("mode1", "MODE1/2352", 1, 0);
    var iso = Path.Combine(root, "mode1.iso");
    var sectors = await LnrDataTrackIsoExporter.ExportAsync(files.cue, iso, CancellationToken.None);
    if (sectors != 20 || new FileInfo(iso).Length != 20L * 2048) throw new Exception("Mode1 ISO size invalid");
    var bytes = File.ReadAllBytes(iso);
    if (bytes[0] != 0 || bytes[2048] != 1 || bytes[19 * 2048] != 19)
        throw new Exception("Mode1 ISO payload mismatch");
}

async Task TestMode2Form1Async()
{
    var files = MakeDisc("mode2", "MODE2/2352", 2, 0);
    var iso = Path.Combine(root, "mode2.iso");
    var sectors = await LnrDataTrackIsoExporter.ExportAsync(files.cue, iso, CancellationToken.None);
    if (sectors != 20 || new FileInfo(iso).Length != 20L * 2048) throw new Exception("Mode2 ISO size invalid");
    var bytes = File.ReadAllBytes(iso);
    if (bytes[0] != 0 || bytes[2048] != 1 || bytes[19 * 2048] != 19)
        throw new Exception("Mode2 ISO payload mismatch");
}

async Task ExpectRejectionAsync(string cueMode, byte rawMode, byte submode)
{
    var name = "reject" + cueMode.Replace("/", "_") + submode;
    var files = MakeDisc(name, cueMode, rawMode, submode);
    try
    {
        await LnrDataTrackIsoExporter.ExportAsync(files.cue, Path.Combine(root, name + ".iso"), CancellationToken.None);
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new Exception("Unsafe track was accepted: " + cueMode);
}

async Task TestMultipleTracksRejectedAsync()
{
    var files = MakeDisc("multiple", "MODE1/2352", 1, 0);
    File.AppendAllText(files.cue, "  TRACK 02 AUDIO\n    INDEX 01 00:12:00\n");
    try
    {
        await LnrDataTrackIsoExporter.ExportAsync(files.cue, Path.Combine(root, "multiple.iso"), CancellationToken.None);
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new Exception("Multi-track CUE was accepted as ISO");
}

(string cue, string bin) MakeDisc(string name, string cueMode, byte rawMode, byte submode)
{
    var bin = Path.Combine(root, name + ".bin");
    var cue = Path.Combine(root, name + ".cue");
    using (var output = File.Create(bin))
    {
        for (int i = 0; i < 20; i++)
        {
            var sector = new byte[2352];
            for (int k = 1; k <= 10; k++) sector[k] = 0xFF;
            sector[15] = rawMode;
            int payloadOffset;
            if (rawMode == 2)
            {
                sector[18] = sector[22] = submode;
                payloadOffset = 24;
            }
            else payloadOffset = 16;
            sector[payloadOffset] = (byte)i;
            output.Write(sector);
        }
    }
    File.WriteAllText(cue, "FILE \"" + name + ".bin\" BINARY\n" +
                            "  TRACK 01 " + cueMode + "\n" +
                            "    INDEX 01 00:00:00\n", Encoding.ASCII);
    return (cue, bin);
}