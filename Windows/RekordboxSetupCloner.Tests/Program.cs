using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RekordboxSetupCloner;

// Dependency-free integration checks. All settings, backups and restore points are synthetic.
var baseDirectory = Environment.GetEnvironmentVariable("RBCLONER_TEST_ROOT") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "opencode");
var temporary = Path.Combine(baseDirectory, "rbcloner-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
var passed = 0;
try
{
    Test("Windows default settings location", () =>
        Check(SettingsStore.DefaultDirectory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pioneer", "rekordbox")));

    Test("Mac scalar allowlist parity", () =>
    {
        var swift = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SettingsPolicy.swift"));
        var expected = new Dictionary<string, SettingsGroup>();
        foreach (Match match in Regex.Matches(swift, "add\\(\\.(\\w+),\\s*(?:\"\"\"([\\s\\S]*?)\"\"\"|\"([^\"]*)\")\\)"))
            foreach (var name in (match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                expected[name] = Enum.Parse<SettingsGroup>(match.Groups[1].Value);
        for (var n = 1; n <= 24; n++) expected[$"VideoTouchFxBeatFxAsign_{n}"] = SettingsGroup.visuals;
        for (var n = 1; n <= 6; n++) expected[$"VideoTouchFxColorFxAsign_{n}"] = SettingsGroup.visuals;
        for (var n = 0; n <= 5; n++) expected[$"AIRecommendCriteriaProviderFlag_{n}"] = SettingsGroup.layout;
        expected["PadEditor Assign"] = SettingsGroup.mappings;
        string[] Array(string variable) => Regex.Matches(Regex.Match(swift, $@"{variable} (?:in|=) \[([\s\S]*?)\]").Groups[1].Value, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        foreach (var panel in Array("panel")) expected[$"ShowPanel_{panel}"] = SettingsGroup.layout;
        for (var d = 0; d <= 3; d++)
        {
            foreach (var suffix in new[] { "WaveViewRatio", "WaveViewRatioDJ", "WaveViewRatioMPL" }) expected[$"Player{d}_{suffix}"] = SettingsGroup.waveforms;
            foreach (var suffix in new[] { "MasterTempo", "TimeMode", "AutoBeatLoopID_PlayerControllPanel" }) expected[$"Player{d}_{suffix}"] = SettingsGroup.performance;
        }
        for (var d = 1; d <= 4; d++)
            foreach (var key in new[] { $"AutoCueEnableDeck{d}", $"Player{d}_Quantize", $"Player{d}_BeatJumpBeat", $"BeatJumpPageDeck{d}" }) expected[key] = SettingsGroup.performance;
        foreach (var color in Array("colors")) expected[$"PadFxColor{color}"] = SettingsGroup.effects;
        Check(expected.Count > 300, "Failed to read Swift policy.");
        Check(expected.Count == SettingsPolicy.PropertyGroups.Count && expected.All(kv => SettingsPolicy.PropertyGroups.GetValueOrDefault(kv.Key) == kv.Value), "Windows and Mac allowlists differ.");
    });

    Test("Filtered export, merged import, mappings and byte-for-byte recovery", () =>
    {
        var (source, target, store) = Fixture("roundtrip");
        var original = Snapshot(target);
        var profile = store.Capture(source, "Party & <friends>", GroupInfo.DefaultSelection);
        Check(profile.Properties[SettingsPolicy.MainFile]["PartAnalysisQuality"] == "1");
        Check(!profile.Properties[SettingsPolicy.MainFile].ContainsKey("AccountToken"));
        Check(!profile.Properties[SettingsPolicy.MainFile].ContainsKey("AudioDevice"));
        Check(!profile.Properties[SettingsPolicy.PlayFile].ContainsKey("CurrentTempo0"));
        Check(!profile.Properties[SettingsPolicy.SamplerFile].ContainsKey("Track"));
        Check(profile.Properties[SettingsPolicy.SamplerFile]["SamplerSet/master_bpm"] == "128");
        var backup = Path.Combine(temporary, "party.rbsetup");
        store.SaveProfile(profile, backup, source);
        var json = File.ReadAllText(backup);
        Check(!json.Contains("SOURCE-SECRET") && !json.Contains("source-song") && !json.Contains("source-playlist") && !json.Contains("source-media"));
        using var encoded = JsonDocument.Parse(json);
        Check(Regex.IsMatch(encoded.RootElement.GetProperty("createdAt").GetString()!, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$"));
        Check(encoded.RootElement.GetProperty("files").GetProperty("MidiMappings/controller.midi.csv").ValueKind == JsonValueKind.String);
        var imported = store.LoadProfile(backup);
        var plan = store.Plan(imported, target, GroupInfo.DefaultSelection);
        Check(plan.Edits.Count >= 8);
        var recovery = store.Apply(plan);
        var values = SettingsXml.Scalars(File.ReadAllBytes(Path.Combine(target, SettingsPolicy.MainFile)));
        Check(values["AccountToken"] == "TARGET-SECRET" && values["AudioDevice"] == "target-device");
        Check(values["PartAnalysisQuality"] == "1" && values["MenuFontName"] == "A & <B> \"C\"");
        Check(File.ReadAllText(Path.Combine(target, SettingsPolicy.SamplerFile)).Contains("target-song"));
        Check(File.ReadAllText(Path.Combine(target, SettingsPolicy.BrowserFile)).Contains("target-playlist"));
        Check(File.ReadAllText(Path.Combine(target, SettingsPolicy.GrooveFile)).Contains("target-drum"));
        Check(File.Exists(Path.Combine(target, "MidiMappings", "extra.midi.csv")));
        Check(File.ReadAllText(Path.Combine(target, "MidiMappings", "shared.midi.csv")) == "source mapping");
        var archive = store.LoadRecovery(recovery, target);
        using var recoveryJson = JsonDocument.Parse(File.ReadAllBytes(recovery));
        Check(recoveryJson.RootElement.GetProperty("entries")[0].TryGetProperty("appliedSHA256", out _));
        var preview = store.RecoveryEdits(archive, target);
        Check(preview.Changed.Count == 0);
        var undoRestore = store.Restore(preview.Edits, target);
        Check(SnapshotsEqual(original, Snapshot(target)), "Restoration did not preserve original bytes or absent mappings.");
        Check(File.Exists(undoRestore));
        Check(store.RecoveryEdits(archive, target).Edits.Count == 0);
        Reject(() => store.LoadRecovery(recovery, source));
        var rules = new DirectoryInfo(store.RecoveryDirectory).GetAccessControl();
        Check(rules.AreAccessRulesProtected);
        var user = WindowsIdentity.GetCurrent().User!;
        Check(rules.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().All(r => r.IdentityReference == user));
        var fileRules = new FileInfo(recovery).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();
        Check(fileRules.All(r => r.IdentityReference == user));
    });

    Test("Major version, missing-key/effects skipping and pad version mismatch", () =>
    {
        var (source, target, store) = Fixture("versions");
        var profile = store.Capture(source, "Version checks", GroupInfo.DefaultSelection);
        Reject(() => store.Plan(profile with { RekordboxVersion = new RekordboxVersion("6.8.0") }, target, GroupInfo.DefaultSelection));
        var main = SettingsXml.Parse(File.ReadAllBytes(Path.Combine(target, SettingsPolicy.MainFile)));
        SettingsXml.Elements(main)["MenuFontName"].Remove();
        File.WriteAllBytes(Path.Combine(target, SettingsPolicy.MainFile), SettingsXml.Bytes(main));
        File.Delete(Path.Combine(target, "FxUnitSettings7.xml"));
        File.WriteAllText(Path.Combine(target, "pad", "Version"), "different");
        var plan = store.Plan(profile, target, GroupInfo.DefaultSelection);
        Check(plan.Skipped.Any(s => s.StartsWith("MenuFontName:")) && plan.Skipped.Any(s => s.StartsWith("FxUnitSettings7.xml:")));
        Check(!plan.Edits.Any(e => e.Path.StartsWith("pad/")));
    });

    Test("Stale previews, including unchanged files and absent files", () =>
    {
        var (source, target, store) = Fixture("stale");
        var profile = store.Capture(source, "Stale", GroupInfo.DefaultSelection);
        var plan = store.Plan(profile, target, GroupInfo.DefaultSelection);
        File.AppendAllText(Path.Combine(target, "PlaySettings.xml"), " ");
        Reject(() => store.Apply(plan));
        plan = store.Plan(profile, target, GroupInfo.DefaultSelection);
        File.WriteAllText(Path.Combine(target, "MidiMappings", "controller.midi.csv"), "new mapping");
        Reject(() => store.Apply(plan));
        Check(!Directory.Exists(store.RecoveryDirectory), "A stale preview should not create a restore point.");
        var padPlan = store.PlanPadFXCopy(target, 1, 2, [0]);
        File.AppendAllText(Path.Combine(target, SettingsPolicy.MainFile), " ");
        Reject(() => store.Apply(padPlan));
    });

    Test("Pad FX copying: both directions, all banks, no-op, exact recovery", () =>
    {
        foreach (var sourceDeck in new[] { 1, 2 })
            foreach (var modes in new int[][] { [0], [1], [0, 1] })
            {
                var (_, target, store) = Fixture($"pad-{sourceDeck}-{string.Join('-', modes)}");
                var before = File.ReadAllBytes(Path.Combine(target, PadFXSettings.Filename));
                var plan = store.PlanPadFXCopy(target, sourceDeck, 3 - sourceDeck, modes);
                Check(plan.Changes.Count == 16 * modes.Length);
                var recovery = store.Apply(plan);
                var a = PadEntries(before);
                var b = PadEntries(File.ReadAllBytes(Path.Combine(target, PadFXSettings.Filename)));
                foreach (var (identity, old) in a)
                {
                    var (deck, mode, pad) = identity;
                    if (deck == 3 - sourceDeck && modes.Contains(mode))
                    {
                        var expected = new XElement(a[(sourceDeck, mode, pad)]);
                        expected.SetAttributeValue("deckNo", deck);
                        Check(SettingsXml.StructureEqual(expected, b[identity]));
                    }
                    else Check(SettingsXml.StructureEqual(old, b[identity]));
                }
                Check(store.PlanPadFXCopy(target, sourceDeck, 3 - sourceDeck, modes).Edits.Count == 0);
                store.Restore(store.RecoveryEdits(store.LoadRecovery(recovery, target), target).Edits, target);
                Check(SettingsStore.Same(before, File.ReadAllBytes(Path.Combine(target, PadFXSettings.Filename))));
            }
    });

    Test("Pad FX malformed records and incomplete/duplicate slots", () =>
    {
        var data = PadXml();
        var document = SettingsXml.Parse(data);
        document.Descendants("PADFXINFO_500").First().Remove();
        Reject(() => PadFXSettings.Copy(SettingsXml.Bytes(document), 1, 2, [0]));
        document = SettingsXml.Parse(data);
        var entry = document.Descendants("PADFXINFO_500").First();
        entry.AddAfterSelf(new XElement(entry));
        Reject(() => PadFXSettings.Copy(SettingsXml.Bytes(document), 1, 2, [0]));
        document = SettingsXml.Parse(data);
        document.Descendants("PADFXINFO_500").First().Name = "NEW_FORMAT";
        Reject(() => PadFXSettings.Copy(SettingsXml.Bytes(document), 1, 2, [0]));
    });

    Test("Mixed-content merging and strict browser/Groove fragments", () =>
    {
        var (source, target, store) = Fixture("mixed");
        var groups = new HashSet<SettingsGroup> { SettingsGroup.sampler, SettingsGroup.layout, SettingsGroup.groove, SettingsGroup.visuals };
        var profile = store.Capture(source, "Mixed content", groups);
        var before = Snapshot(target);
        var recovery = store.Apply(store.Plan(profile, target, groups));
        var video = SettingsXml.Parse(File.ReadAllBytes(Path.Combine(target, SettingsPolicy.VideoFile)));
        Check(video.Descendants("media").Single().Value == "target-media");
        Check(video.Descendants("size").Single().Attribute("value")!.Value == "75");
        Check(File.ReadAllText(Path.Combine(target, SettingsPolicy.SamplerFile)).Contains("target-song"));
        store.Restore(store.RecoveryEdits(store.LoadRecovery(recovery, target), target).Edits, target);
        Check(SnapshotsEqual(before, Snapshot(target)));
        var key = "TableHeader-CollectionTracks";
        var fragment = SettingsXml.Parse(profile.StructuredPreferences[SettingsPolicy.BrowserFile][key]);
        fragment.Descendants("COLUMN").First().SetAttributeValue("libraryID", "123");
        Reject(() => SettingsXml.ValidatedStructure(SettingsXml.Bytes(fragment), key, SettingsPolicy.BrowserFile));
        fragment = SettingsXml.Parse(profile.StructuredPreferences[SettingsPolicy.BrowserFile][key]);
        fragment.Root!.Elements().Single().Add(new XComment("hidden data"));
        Reject(() => SettingsXml.ValidatedStructure(SettingsXml.Bytes(fragment), key, SettingsPolicy.BrowserFile));
        var browser = SettingsXml.Parse(before[SettingsPolicy.BrowserFile]);
        browser.Descendants("COLUMN").First().SetAttributeValue("id", "999");
        File.WriteAllBytes(Path.Combine(target, SettingsPolicy.BrowserFile), SettingsXml.Bytes(browser));
        var plan = store.Plan(profile, target, groups);
        Check(plan.Skipped.Any(s => s.StartsWith(key + ":")));
        browser = SettingsXml.Parse(before[SettingsPolicy.BrowserFile]);
        SettingsXml.Elements(browser)[key].ReplaceWith(SettingsXml.ValidatedStructure(profile.StructuredPreferences[SettingsPolicy.BrowserFile][key], key, SettingsPolicy.BrowserFile));
        File.WriteAllBytes(Path.Combine(target, SettingsPolicy.BrowserFile), SettingsXml.Bytes(browser));
        Check(!store.Plan(profile, target, groups).Edits.Any(e => e.Path == SettingsPolicy.BrowserFile));
    });

    Test("Swift-shaped version 1/2 archives and sampler remapping", () =>
    {
        var (_, target, store) = Fixture("compatibility");
        var backup = Path.Combine(temporary, "swift-shaped.rbsetup");
        File.WriteAllText(backup, """
            {"format":"rekordbox-setup","schemaVersion":1,"createdAt":"2026-10-08T12:00:00Z",
             "name":"Mac setup","rekordboxVersion":{"raw":"70219"},"groups":["layout"],
             "properties":{"rekordbox3.settings":{"SamplerLayout":"2"}},"files":{}}
            """);
        var profile = store.LoadProfile(backup);
        Check(profile.RekordboxVersion.Display == "7.2.19");
        Check(store.Plan(profile, target, [SettingsGroup.sampler]).Changes.Single().Group == SettingsGroup.sampler);
        Check(store.Plan(profile, target, [SettingsGroup.layout]).Edits.Count == 0);
        File.WriteAllText(backup, """
            {"format":"rekordbox-setup","schemaVersion":2,"createdAt":"2026-10-08T12:00:00Z",
             "name":"Mac setup","rekordboxVersion":{"raw":"7.2.19"},"groups":["mappings"],
             "properties":{},"files":{"MidiMappings/test.midi.csv":"YWJjCg=="},"structures":{}}
            """);
        Check(Encoding.UTF8.GetString(store.LoadProfile(backup).Files["MidiMappings/test.midi.csv"]) == "abc\n");
    });

    Test("Invalid archives, XML declarations, traversal, ADS and case collisions", () =>
    {
        var (source, _, store) = Fixture("invalid");
        var profile = store.Capture(source, "Invalid", GroupInfo.DefaultSelection);
        foreach (var path in new[] { "../bad", "MidiMappings/../bad.midi.csv", "MidiMappings/a.midi.csv:stream", "MidiMappings/CON.midi.csv", "pad/assign1/x.pad.csv.", "MidiMappings/a\\b.midi.csv", "MidiMappings/a.midi.csv " })
        {
            Check(!SettingsPolicy.ValidRelativePath(path));
            Reject(() => SettingsStore.Validate(profile with { Files = new() { [path] = Encoding.UTF8.GetBytes("x") } }));
        }
        Reject(() => SettingsStore.Validate(profile with { Files = new() { ["MidiMappings/a.midi.csv"] = [65], ["MidiMappings/A.midi.csv"] = [66] } }));
        Reject(() => SettingsStore.Validate(profile with { Properties = new() { [SettingsPolicy.MainFile] = new() { ["AccountToken"] = "secret" } } }));
        Reject(() => SettingsStore.Validate(profile with { Properties = new() { [SettingsPolicy.SamplerFile] = new() { ["Track"] = "song" } } }));
        Reject(() => SettingsXml.Parse(Encoding.UTF8.GetBytes("<!DOCTYPE PROPERTIES [<!ENTITY x SYSTEM 'file:///secret'>]><PROPERTIES/>")));
        Reject(() => SettingsXml.Scalars(Encoding.UTF8.GetBytes("<PROPERTIES><VALUE name='x' val='1'/><VALUE name='x' val='2'/></PROPERTIES>")));
        Reject(() => SettingsXml.Parse([255, 254, 0]));
        var malformed = Path.Combine(temporary, "invalid.rbsetup");
        File.WriteAllText(malformed, "{\"format\":\"a\",\"format\":\"b\"}");
        Reject(() => store.LoadProfile(malformed));
        File.WriteAllText(malformed, "{\"format\":\"rekordbox-setup\",\"schemaVersion\":2,\"name\":\"a\"}");
        Reject(() => store.LoadProfile(malformed));
        Reject(() => store.SaveProfile(profile, Path.Combine(source, "bad.rbsetup"), source));
    });

    Test("Windows junctions cannot redirect reads or writes", () =>
    {
        var (source, target, store) = Fixture("junction");
        var alias = Path.Combine(temporary, "settings-junction");
        MakeJunction(alias, target);
        try { Reject(() => store.Inspect(alias)); }
        finally { Directory.Delete(alias); }
        var profile = store.Capture(source, "Junction", GroupInfo.DefaultSelection);
        Directory.Delete(Path.Combine(target, "MidiMappings"), true);
        MakeJunction(Path.Combine(target, "MidiMappings"), Path.Combine(source, "MidiMappings"));
        try { Reject(() => store.Plan(profile, target, GroupInfo.DefaultSelection)); }
        finally { Directory.Delete(Path.Combine(target, "MidiMappings")); }
    });

    Test("File/archive size limits and malformed archive values", () =>
    {
        var (source, _, store) = Fixture("limits");
        var oversized = Path.Combine(temporary, "oversized.rbsetup");
        using (var stream = File.Create(oversized)) stream.SetLength(SettingsPolicy.MaxArchiveSize + 1L);
        Reject(() => store.LoadProfile(oversized));
        using (var stream = File.Create(Path.Combine(source, "MidiMappings", "large.midi.csv"))) stream.SetLength(SettingsPolicy.MaxFileSize + 1L);
        Reject(() => store.Capture(source, "Too large", [SettingsGroup.mappings]));
        File.Delete(Path.Combine(source, "MidiMappings", "large.midi.csv"));
        var profile = store.Capture(source, "Invalid values", GroupInfo.DefaultSelection);
        var json = JsonSerializer.Serialize(profile, ArchiveJson.Options);
        File.WriteAllText(oversized, json.Replace("\"stems\"", "0", StringComparison.Ordinal));
        Reject(() => store.LoadProfile(oversized));
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["files"]!["MidiMappings/shared.midi.csv"] = "not base64!!";
        File.WriteAllText(oversized, node.ToJsonString());
        Reject(() => store.LoadProfile(oversized));
    });

    Test("Partial write failure rolls back and retains persisted recovery", () =>
    {
        var (source, target, store) = Fixture("rollback");
        var profile = store.Capture(source, "Rollback", [SettingsGroup.stems, SettingsGroup.performance]);
        var plan = store.Plan(profile, target, [SettingsGroup.stems, SettingsGroup.performance]);
        Check(plan.Edits.Count >= 2);
        var before = Snapshot(target);
        using (var locked = new FileStream(Path.Combine(target, plan.Edits[1].Path), FileMode.Open, FileAccess.Read, FileShare.Read))
            Reject(() => store.Apply(plan));
        Check(SnapshotsEqual(before, Snapshot(target)), "Rollback did not restore the first edited file.");
        Check(Directory.GetFiles(store.RecoveryDirectory, "*.rbrecovery").Length == 1);
    });

    Test("Later preference edits are flagged before restoration", () =>
    {
        var (source, target, store) = Fixture("later-edits");
        var profile = store.Capture(source, "Later edits", [SettingsGroup.stems]);
        var recovery = store.Apply(store.Plan(profile, target, [SettingsGroup.stems]));
        File.AppendAllText(Path.Combine(target, SettingsPolicy.MainFile), "\n ");
        var archive = store.LoadRecovery(recovery, target);
        var preview = store.RecoveryEdits(archive, target);
        Check(preview.Changed.SequenceEqual([SettingsPolicy.MainFile]));
        store.Restore(preview.Edits, target);
        Check(store.RecoveryEdits(archive, target).Edits.Count == 0);
    });

    Test("Running rekordbox process blocks modifying operations", () =>
    {
        var (source, target, store) = Fixture("process");
        var exe = Path.Combine(temporary, "rekordbox.exe");
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), exe);
        using var process = Process.Start(new ProcessStartInfo(exe, "/c pause") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true })!;
        try
        {
            Check(!process.HasExited);
            Reject(() => store.Capture(source, "Blocked", [SettingsGroup.stems]));
            Reject(() => store.PlanPadFXCopy(target, 1, 2, [0]));
        }
        finally { if (!process.HasExited) process.Kill(true); process.WaitForExit(); }
    });

    Console.WriteLine($"PASS: {passed} synthetic integration checks. No installed rekordbox settings were accessed.");
}
finally { Directory.Delete(temporary, true); }

void Test(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
static void Check(bool condition, string message = "Assertion failed.") { if (!condition) throw new Exception(message); }
static void Reject(Action action)
{
    try { action(); }
    catch (Exception error) when (error is SetupException or JsonException or System.Xml.XmlException or IOException) { return; }
    throw new Exception("Expected the operation to be rejected.");
}
static Dictionary<string, byte[]> Snapshot(string folder) => Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(p => Path.GetRelativePath(folder, p).Replace('\\', '/'), File.ReadAllBytes);
static bool SnapshotsEqual(Dictionary<string, byte[]> a, Dictionary<string, byte[]> b) => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var bytes) && SettingsStore.Same(kv.Value, bytes));
static Dictionary<(int Deck, int Mode, int Pad), XElement> PadEntries(byte[] data) => SettingsXml.Parse(data).Descendants("PADFXINFO_500").ToDictionary(e => (int.Parse(e.Attribute("deckNo")!.Value), int.Parse(e.Attribute("modeIndex")!.Value), int.Parse(e.Attribute("padIndex")!.Value)));
static byte[] PadXml()
{
    var container = new XElement("PadFXSettings");
    for (var d = 1; d <= 4; d++)
        for (var m = 0; m <= 1; m++)
            for (var p = 0; p < 16; p++)
                container.Add(new XElement("PADFXINFO_500", new XAttribute("deckNo", d), new XAttribute("modeIndex", m), new XAttribute("padIndex", p),
                    new XAttribute("effect", $"{d}-{m}-{p}"), new XAttribute("colour", d * 10 + p), new XAttribute("hold", d % 2)));
    return SettingsXml.Bytes(new XDocument(new XElement("PROPERTIES", new XElement("VALUE", new XAttribute("name", "PadFXSettings"), container))));
}
static void MakeJunction(string path, string target)
{
    using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{path}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
    process.WaitForExit();
    Check(process.ExitCode == 0, "Could not create a synthetic junction.");
}
(string Source, string Target, SettingsStore Store) Fixture(string name)
{
    var root = Path.Combine(temporary, name);
    var source = Path.Combine(root, "source");
    var target = Path.Combine(root, "target");
    foreach (var (folder, sourceSide) in new[] { (source, true), (target, false) })
    {
        Directory.CreateDirectory(folder);
        var side = sourceSide ? "source" : "target";
        var n = sourceSide ? "1" : "0";
        var main = new XDocument(new XElement("PROPERTIES",
            new XElement("VALUE", new XAttribute("name", "LaunchedVersion"), new XAttribute("val", "70219")),
            new XElement("VALUE", new XAttribute("name", "TrackSeparationEnable"), new XAttribute("val", n)),
            new XElement("VALUE", new XAttribute("name", "PartAnalysisQuality"), new XAttribute("val", n)),
            new XElement("VALUE", new XAttribute("name", "MenuFontName"), new XAttribute("val", sourceSide ? "A & <B> \"C\"" : "Segoe UI")),
            new XElement("VALUE", new XAttribute("name", "SamplerLayout"), new XAttribute("val", n)),
            new XElement("VALUE", new XAttribute("name", "AccountToken"), new XAttribute("val", side.ToUpperInvariant() + "-SECRET")),
            new XElement("VALUE", new XAttribute("name", "AudioDevice"), new XAttribute("val", side + "-device"))));
        File.WriteAllBytes(Path.Combine(folder, SettingsPolicy.MainFile), SettingsXml.Bytes(main));
        File.WriteAllText(Path.Combine(folder, SettingsPolicy.PlayFile), $"<PROPERTIES><VALUE name='Vinyl0' val='{n}'/><VALUE name='CurrentTempo0' val='131'/></PROPERTIES>");
        File.WriteAllText(Path.Combine(folder, SettingsPolicy.SamplerFile), $"<PROPERTIES><VALUE name='sync' val='{n}'/><VALUE name='SamplerSet'><SamplerSet><VALUE name='master_bpm' val='{(sourceSide ? 128 : 120)}'/><Track url='{side}-song' libraryID='77'/></SamplerSet></VALUE></PROPERTIES>");
        File.WriteAllText(Path.Combine(folder, SettingsPolicy.BrowserFile), $"<PROPERTIES><VALUE name='TableHeader-CollectionTracks'><TABLELAYOUT sortedCol='1' sortForwards='1'><COLUMN id='1' visible='1' width='{(sourceSide ? 150 : 100)}'/></TABLELAYOUT></VALUE><VALUE name='TreeSelection' val='{side}-playlist'/></PROPERTIES>");
        File.WriteAllText(Path.Combine(folder, SettingsPolicy.GrooveFile), $"<PROPERTIES><VALUE name='visibleBank1' val='{n}'/><VALUE name='FxSet1'><FxSet1><Beat idx='0' numerator='{(sourceSide ? 2 : 1)}' denominator='4'/></FxSet1></VALUE><VALUE name='Track' val='{side}-drum'/></PROPERTIES>");
        File.WriteAllText(Path.Combine(folder, SettingsPolicy.VideoFile), $"<PROPERTIES><VALUE name='ImageOverlay'><ImageOverlay><size value='{(sourceSide ? 75 : 50)}'/><transparency value='{n}'/><media>{side}-media</media></ImageOverlay></VALUE></PROPERTIES>");
        File.WriteAllText(Path.Combine(folder, "FxUnitSettings7.xml"), $"<PROPERTIES><VALUE name='fx' val='{n}'/></PROPERTIES>");
        File.WriteAllBytes(Path.Combine(folder, PadFXSettings.Filename), PadXml());
        Directory.CreateDirectory(Path.Combine(folder, "pad"));
        File.WriteAllText(Path.Combine(folder, "pad", "Version"), "1");
        File.WriteAllText(Path.Combine(folder, "pad", "custom.pad.csv"), side + " pad");
        Directory.CreateDirectory(Path.Combine(folder, "MidiMappings"));
        File.WriteAllText(Path.Combine(folder, "MidiMappings", "shared.midi.csv"), side + " mapping");
        Directory.CreateDirectory(Path.Combine(folder, "KeyMappings"));
        File.WriteAllText(Path.Combine(folder, "KeyMappings", "preset.mappings"), $"<PROPERTIES><VALUE name='shortcut' val='{n}'/></PROPERTIES>");
        Directory.CreateDirectory(Path.Combine(folder, "pad", "assign1"));
        File.WriteAllText(Path.Combine(folder, "pad", "assign1", "custom.pad.csv"), side + " assignment");
        if (sourceSide) File.WriteAllText(Path.Combine(folder, "MidiMappings", "controller.midi.csv"), "controller mapping");
        else File.WriteAllText(Path.Combine(folder, "MidiMappings", "extra.midi.csv"), "keep local mapping");
    }
    return (source, target, new SettingsStore(Path.Combine(root, "recovery")));
}
