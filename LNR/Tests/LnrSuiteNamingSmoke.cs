using System;
using System.IO;
using System.Linq;
using PSXPackagerGUI;

static void Assert(bool expression, string message)
{
    if (!expression) throw new InvalidOperationException("Suite names: " + message);
}
var root = Path.Combine(Path.GetTempPath(), "LnrSuiteNamingSmoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Assert(LnrSuiteDiscNaming.DiscName("Final Fantasy VII.pbp", 3, 2)
           == "Final Fantasy VII (Disc 2)", "multidisc naming");
    Assert(LnrSuiteDiscNaming.DiscName("Final Fantasy VII (Disc 1).pbp", 3, 2)
           == "Final Fantasy VII (Disc 2)", "avoid doubled disc suffix");
    Assert(LnrSuiteDiscNaming.DiscName("Final Fantasy VII - Disc 1.pbp", 3, 3)
           == "Final Fantasy VII (Disc 3)", "remove legacy disc suffix");
    Assert(LnrSuiteDiscNaming.DiscName("Single Game.pbp", 1, 1)
           == "Single Game", "single disc should not gain a suffix");
    Assert(LnrSuiteDiscNaming.ChdNames("Title.pbp", 4).SequenceEqual(new[] {
        "Title (Disc 1).chd", "Title (Disc 2).chd",
        "Title (Disc 3).chd", "Title (Disc 4).chd"
    }), "all four CHD names in correct internal order");

    // Filenames deliberately unsorted. The actual extractor uses "- Disc N".
    var stage = Path.Combine(root, "stage");
    Directory.CreateDirectory(stage);
    foreach (var num in new[] { 3, 1, 2 })
    {
        var stem = "Final Fantasy VII - Disc " + num;
        File.WriteAllBytes(Path.Combine(stage, stem + ".bin"), new byte[4096]);
        File.WriteAllText(Path.Combine(stage, stem + ".cue"),
            "FILE \"" + stem + ".bin\" BINARY\r\n" +
            "  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n" +
            "  TRACK 02 AUDIO\r\n    INDEX 01 03:00:00\r\n");
    }
    var before = LnrSuiteDiscNaming.ExtractedCues(stage, 3).Select(Path.GetFileName).ToArray();
    Assert(before.SequenceEqual(new[] {
        "Final Fantasy VII - Disc 1.cue", "Final Fantasy VII - Disc 2.cue",
        "Final Fantasy VII - Disc 3.cue"
    }), "disc indices not alphabetical order");
    LnrSuiteDiscNaming.NormalizeBinCue(stage, "Final Fantasy VII.pbp", 3);
    foreach (var num in new[] { 1, 2, 3 })
    {
        var stem = "Final Fantasy VII (Disc " + num + ")";
        var cue = Path.Combine(stage, stem + ".cue");
        var bin = Path.Combine(stage, stem + ".bin");
        Assert(File.Exists(cue) && File.Exists(bin), "new matching CUE/BIN missing for " + num);
        var text = File.ReadAllText(cue);
        Assert(text.Contains("FILE \"" + stem + ".bin\" BINARY"), "CUE not rewritten for " + num);
        Assert(text.Contains("TRACK 02 AUDIO"), "Audio track not preserved for " + num);
        Assert(!File.Exists(Path.Combine(stage, "Final Fantasy VII - Disc " + num + ".cue")),
               "old CUE remains");
    }

    // Publish all CHDs or skip entire group if ANY pre-existing file exists.
    var chdStage = Path.Combine(root, "CHD-Stage");
    var dest = Path.Combine(root, "CHD");
    Directory.CreateDirectory(chdStage);
    Directory.CreateDirectory(dest);
    var names = LnrSuiteDiscNaming.ChdNames("Title.pbp", 3);
    foreach (var name in names) File.WriteAllBytes(Path.Combine(chdStage, name), new byte[4096]);
    var existing = Path.Combine(dest, names[1]);
    File.WriteAllText(existing, "DO NOT OVERWRITE");
    Assert(!LnrSuiteDiscNaming.PublishChdGroup(chdStage, dest, names),
           "must skip entire group if one file exists");
    Assert(File.ReadAllText(existing) == "DO NOT OVERWRITE", "existing output changed");
    Assert(!File.Exists(Path.Combine(dest, names[0])), "partial group published");
    File.Delete(existing);

    // Simulate a failure after the first CHD move: a directory blocks
    // the second filename. Only newly committed outputs are rolled back.
    var blocker = Path.Combine(dest, names[1]);
    Directory.CreateDirectory(blocker);
    var threw = false;
    try { _ = LnrSuiteDiscNaming.PublishChdGroup(chdStage, dest, names); }
    catch (IOException) { threw = true; }
    Assert(threw, "must fail when a destination name is a directory");
    Assert(!File.Exists(Path.Combine(dest, names[0])), "rollback of first committed CHD failed");
    Assert(Directory.Exists(blocker), "a pre-existing directory was removed");
    Directory.Delete(blocker);

    // A rolled-back CHD is intentionally DELETED (never put back into a
    // possibly incomplete staging tree). Recreate the synthetic input for
    // the independent successful-commit scenario.
    foreach (var name in names)
        if (!File.Exists(Path.Combine(chdStage, name)))
            File.WriteAllBytes(Path.Combine(chdStage, name), new byte[4096]);

    Assert(LnrSuiteDiscNaming.PublishChdGroup(chdStage, dest, names),
           "publish all three validated CHDs");
    Assert(names.All(name => File.Exists(Path.Combine(dest, name))),
           "not all CHD outputs published");

    // A final CHD failing verification must undo EVERY new output from
    // that PBP, not leave a misleading partially published group.
    var postStage = Path.Combine(root, "Verify-Stage");
    var postDest = Path.Combine(root, "Verify-Destination");
    Directory.CreateDirectory(postStage);
    Directory.CreateDirectory(postDest);
    var checkNames = LnrSuiteDiscNaming.ChdNames("Verify.pbp", 2);
    foreach (var name in checkNames)
        File.WriteAllBytes(Path.Combine(postStage, name), new byte[4096]);
    var postFailed = false;
    try
    {
        LnrSuiteDiscNaming.PublishChdGroup(postStage, postDest, checkNames,
            path =>
            {
                if (path.EndsWith("(Disc 2).chd", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Synthetic final verification failure");
            });
    }
    catch (InvalidDataException) { postFailed = true; }
    Assert(postFailed, "final verification error must propagate");
    Assert(checkNames.All(name => !File.Exists(Path.Combine(postDest, name))),
           "final validation failure left a partially published PBP group");
    Console.WriteLine("Suite multidisc smoke: ordered disc numbers, CUE/BIN references, audio tracks, " +
                      "skip on conflict, rollback, and 3-disc commit PASSED.");
}
finally
{
    Directory.Delete(root, recursive: true);
}
