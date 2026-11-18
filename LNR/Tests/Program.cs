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
    await TestMode2Form1RejectedAsync();
    TestPreflightMode1();
    TestPreflightRejectsMode2();
    TestPreflightRejectsMixedSectors();
    TestPreflightRejectsMultiTrack();
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
    Console.WriteLine("LNR ISO smoke tests: MODE1-only export, MODE2/audio/multitrack rejection, and MODE1 preflight passed.");
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

async Task TestMode2Form1RejectedAsync()
{
    // MODE2/Form1 was technically exportable in older experimental Suite
    // versions. Beta.32 deliberately rejects it under the strict MODE1-only
    // user-facing policy so it cannot be silently converted by mistake.
    await ExpectRejectionAsync("MODE2/2352", 2, 0);
}

void TestPreflightMode1()
{
    var data = File.ReadAllBytes(MakeDisc("preflight1", "MODE1/2352", 1, 0).bin);
    using var raw = new MemoryStream(data);
    LnrDataTrackIsoExporter.CheckPbpMode1Preflight(raw, true);
}

void TestPreflightRejectsMode2()
{
    var data = File.ReadAllBytes(MakeDisc("preflight2", "MODE2/2352", 2, 0).bin);
    using var raw = new MemoryStream(data);
    ExpectPreflightRejection(raw, true, "MODE2");
}

void TestPreflightRejectsMixedSectors()
{
    var data = File.ReadAllBytes(MakeDisc("mixed", "MODE1/2352", 1, 0).bin);
    // A late incompatible sector must reject the disc at Analyze when
    // sampled. Every sector is still checked during the actual export.
    data[15 * 2352 + 15] = 2;
    using var raw = new MemoryStream(data);
    ExpectPreflightRejection(raw, true, "MODE2");
}

void TestPreflightRejectsMultiTrack()
{
    var data = File.ReadAllBytes(MakeDisc("multi", "MODE1/2352", 1, 0).bin);
    using var raw = new MemoryStream(data);
    ExpectPreflightRejection(raw, false, "pista");
}

void ExpectPreflightRejection(Stream stream, bool singleDataTrack, string expected)
{
    try
    {
        LnrDataTrackIsoExporter.CheckPbpMode1Preflight(stream, singleDataTrack);
    }
    catch (InvalidDataException ex) when (ex.Message.Contains(expected,
            StringComparison.OrdinalIgnoreCase))
    {
        return;
    }
    throw new Exception("MODE1-only preflight did not reject " + expected);
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