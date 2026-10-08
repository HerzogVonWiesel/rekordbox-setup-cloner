using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace RekordboxSetupCloner;

public sealed class SettingsStore
{
    private static string PioneerDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pioneer");
    public static string DefaultDirectory => FindSettingsDirectory(PioneerDirectory);
    public static string DefaultRecoveryDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rekordbox Setup Cloner", "Restore Points");
    public string RecoveryDirectory { get; }
    public SettingsStore(string? recoveryDirectory = null) => RecoveryDirectory = Path.GetFullPath(recoveryDirectory ?? DefaultRecoveryDirectory);

    public static string FindSettingsDirectory(string pioneerDirectory)
    {
        pioneerDirectory = Path.GetFullPath(pioneerDirectory);
        foreach (var name in new[] { "rekordbox6", "rekordbox" })
        {
            var candidate = Path.Combine(pioneerDirectory, name);
            if (File.Exists(Path.Combine(candidate, SettingsPolicy.MainFile))) return candidate;
        }
        return Path.Combine(pioneerDirectory, "rekordbox6");
    }

    public static void RequireRekordboxClosed()
    {
        var running = false;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
                try
                {
                    if (Regex.IsMatch(process.ProcessName, @"^rekordbox(?:\s*[0-9]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) running = true;
                }
                catch (InvalidOperationException) { /* The process exited while enumerating. */ }
        }
        if (running) throw new SetupException("Quit rekordbox before saving, importing, copying Pad FX, or restoring settings. This also lets rekordbox finish saving your preferences.");
    }

    private static bool Within(string path, string directory) =>
        path.Equals(directory, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    // Windows junctions and other reparse points can redirect writes just like symbolic links.
    private static void RejectReparsePoints(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new SetupException($"Symbolic links and junctions are not supported: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static string Root(string directory)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        RejectReparsePoints(path);
        if (!Directory.Exists(path)) throw new SetupException("The settings location is not a folder.");
        return path;
    }

    private static string FilePath(string relative, string directory)
    {
        if (!SettingsPolicy.ValidRelativePath(relative)) throw new SetupException("Invalid relative settings path.");
        var path = Path.GetFullPath(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!Within(path, directory) || path.Equals(directory, StringComparison.OrdinalIgnoreCase)) throw new SetupException($"Settings path leaves the selected folder: {relative}");
        RejectReparsePoints(path);
        return path;
    }

    private static byte[] BoundedData(string path, int maximum)
    {
        RejectReparsePoints(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximum) throw new SetupException($"File exceeds the size limit: {Path.GetFileName(path)}");
        // Bound the read even if a file grows while being read.
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            if (output.Length + count > maximum) throw new SetupException("File exceeds the size limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static byte[]? Read(string relative, string directory)
    {
        var path = FilePath(relative, directory);
        try { return BoundedData(path, SettingsPolicy.MaxFileSize); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public static bool Same(byte[]? a, byte[]? b) => a == null ? b == null : b != null && a.AsSpan().SequenceEqual(b);
    private static string? Digest(byte[]? data) => data == null ? null : Convert.ToHexStringLower(SHA256.HashData(data));

    private static byte[] MainSettings(string directory) => Read(SettingsPolicy.MainFile, directory) ??
        throw new SetupException($"The selected folder does not contain rekordbox3.settings:\n{directory}\n\nChoose the folder containing that file. Windows installations commonly use %APPDATA%\\Pioneer\\rekordbox6. Open and quit rekordbox once if its settings have not been created yet.");

    private static RekordboxVersion Version(byte[] main)
    {
        var raw = SettingsXml.Scalars(main).GetValueOrDefault("LaunchedVersion", "").Trim();
        // Informational metadata only. Compatibility is checked against destination settings.
        return new RekordboxVersion(raw.Length <= 64 && !raw.Any(char.IsControl) ? raw : "");
    }

    private static List<string> AuxiliaryPaths(string directory)
    {
        var paths = new List<string>();
        void Visit(string folder, int depth)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(folder))
            {
                var name = Path.GetFileName(path);
                if (name.StartsWith('.')) continue;
                var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new SetupException($"Symbolic links and junctions are not supported: {relative}");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (depth == 0 && new[] { "KeyMappings", "MidiMappings", "pad" }.Contains(name)) Visit(path, 1);
                    else if (depth == 1 && relative.StartsWith("pad/", StringComparison.Ordinal) && Regex.IsMatch(name, @"^assign[0-9]+$")) Visit(path, 2);
                }
                else if (SettingsPolicy.FileGroup(relative) != null) paths.Add(relative);
                if (paths.Count > SettingsPolicy.MaxFiles) throw new SetupException("Too many mapping files.");
            }
        }
        Visit(directory, 0);
        return paths.Order(StringComparer.Ordinal).ToList();
    }

    public SettingsInspection Inspect(string directory)
    {
        directory = Root(directory);
        var main = MainSettings(directory);
        var values = SettingsXml.Scalars(main);
        var counts = new Dictionary<SettingsGroup, int>();
        void Count(SettingsGroup? group) { if (group is { } g) counts[g] = counts.GetValueOrDefault(g) + 1; }
        foreach (var file in SettingsPolicy.PreferenceFiles)
        {
            if (Read(file, directory) is not { } data) continue;
            foreach (var key in SettingsXml.Scalars(data, file).Keys) Count(SettingsPolicy.Group(key, file));
            foreach (var key in SettingsXml.Structures(data, file).Keys) Count(SettingsPolicy.StructureGroup(key, file));
        }
        foreach (var path in AuxiliaryPaths(directory)) Count(SettingsPolicy.FileGroup(path));
        return new SettingsInspection(Version(main), counts, values.TryGetValue("TrackSeparationEnable", out var stems) ? stems == "1" : null);
    }

    public SetupProfile Capture(string directory, string name, HashSet<SettingsGroup> groups)
    {
        RequireRekordboxClosed();
        if (groups.Count == 0) throw new SetupException("Select at least one settings group.");
        directory = Root(directory);
        var main = MainSettings(directory);
        var properties = new Dictionary<string, Dictionary<string, string>>();
        var structures = new Dictionary<string, Dictionary<string, byte[]>>();
        foreach (var file in SettingsPolicy.PreferenceFiles)
        {
            if (!SettingsPolicy.Groups(file).Overlaps(groups)) continue;
            if (Read(file, directory) is not { } data) continue;
            var values = SettingsXml.Scalars(data, file).Where(kv => SettingsPolicy.Group(kv.Key, file) is { } g && groups.Contains(g)).ToDictionary();
            if (values.Count > 0) properties[file] = values;
            var fragments = SettingsXml.Structures(data, file).Where(kv => SettingsPolicy.StructureGroup(kv.Key, file) is { } g && groups.Contains(g)).ToDictionary();
            if (fragments.Count > 0) structures[file] = fragments;
        }
        var files = new Dictionary<string, byte[]>();
        foreach (var path in AuxiliaryPaths(directory))
            if (SettingsPolicy.FileGroup(path) is { } g && groups.Contains(g))
                files[path] = Read(path, directory) ?? throw new SetupException("Settings changed while being read. Try again.");
        var profile = new SetupProfile("rekordbox-setup", 2, DateTimeOffset.UtcNow, name.Trim(), Version(main),
            GroupInfo.All.Where(groups.Contains).ToArray(), properties, files, structures);
        Validate(profile);
        RequireRekordboxClosed();
        return profile;
    }

    private static void ValidateAuxiliary(byte[] data, string path)
    {
        if (data.Length > SettingsPolicy.MaxFileSize) throw new SetupException($"Settings file too large: {path}");
        if (path.EndsWith(".xml", StringComparison.Ordinal) || path.EndsWith(".mappings", StringComparison.Ordinal)) _ = SettingsXml.Parse(data);
        else
        {
            try
            {
                if (new UTF8Encoding(false, true).GetString(data).Contains('\0')) throw new SetupException($"Unsupported mapping encoding: {path}");
            }
            catch (DecoderFallbackException) { throw new SetupException($"Unsupported mapping encoding: {path}"); }
        }
    }

    public static void Validate(SetupProfile profile)
    {
        if (profile.Format != "rekordbox-setup" || profile.SchemaVersion is not (1 or 2) ||
            profile.SchemaVersion == 1 && profile.StructuredPreferences.Count != 0 || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 120 ||
            profile.RekordboxVersion?.Raw == null || profile.RekordboxVersion.Raw.Length > 64 || profile.RekordboxVersion.Raw.Any(char.IsControl) ||
            profile.Groups == null || profile.Groups.Length == 0 ||
            profile.Groups.Any(g => !Enum.IsDefined(g)) || profile.Groups.Distinct().Count() != profile.Groups.Length ||
            profile.Properties == null || profile.Files == null || profile.Files.Count > SettingsPolicy.MaxFiles)
            throw new SetupException("This is not a supported setup backup.");
        foreach (var (file, properties) in profile.Properties)
        {
            if (properties == null || !SettingsPolicy.PreferenceFiles.Contains(file) || profile.SchemaVersion == 1 && file != SettingsPolicy.MainFile && file != SettingsPolicy.PlayFile)
                throw new SetupException($"Unapproved settings file: {file}");
            foreach (var (key, value) in properties)
                if (SettingsPolicy.Group(key, file, profile.SchemaVersion) is not { } group || !profile.Groups.Contains(group) ||
                    value == null || Encoding.UTF8.GetByteCount(value) > 4096 || value.Contains('\0'))
                    throw new SetupException($"Unapproved or invalid preference: {key}");
        }
        foreach (var (file, fragments) in profile.StructuredPreferences)
        {
            if (fragments == null || !SettingsPolicy.PreferenceFiles.Contains(file)) throw new SetupException($"Unapproved preference file: {file}");
            foreach (var (key, data) in fragments)
            {
                if (data == null || SettingsPolicy.StructureGroup(key, file) is not { } group || !profile.Groups.Contains(group) ||
                    profile.Properties.GetValueOrDefault(file)?.ContainsKey(key) == true) throw new SetupException($"Unapproved or duplicate preference: {key}");
                _ = SettingsXml.ValidatedStructure(data, key, file);
            }
        }
        if (profile.Files.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != profile.Files.Count) throw new SetupException("Backup contains colliding Windows filenames.");
        foreach (var (path, data) in profile.Files)
        {
            if (data == null || SettingsPolicy.FileGroup(path) is not { } group || !profile.Groups.Contains(group)) throw new SetupException($"Unapproved file in backup: {path}");
            ValidateAuxiliary(data, path);
        }
        if (profile.PreferenceCount + profile.Files.Count == 0) throw new SetupException("This backup contains no settings.");
    }

    private static T LoadArchive<T>(string path, JsonTypeInfo<T> metadata)
    {
        var bytes = BoundedData(path, SettingsPolicy.MaxArchiveSize);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        void CheckDuplicates(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in element.EnumerateObject())
                {
                    if (!names.Add(p.Name)) throw new SetupException($"Duplicate archive field: {p.Name}");
                    CheckDuplicates(p.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
        }
        CheckDuplicates(document.RootElement);
        return document.Deserialize(metadata) ?? throw new SetupException("Invalid archive.");
    }

    public SetupProfile LoadProfile(string path)
    {
        var profile = LoadArchive(path, ArchiveJson.ProfileInfo);
        Validate(profile);
        return profile;
    }

    public void SaveProfile(SetupProfile profile, string path, string settingsDirectory)
    {
        Validate(profile);
        path = Path.GetFullPath(path);
        RejectReparsePoints(path);
        if (Within(path, PioneerDirectory) || Within(path, Root(settingsDirectory)))
            throw new SetupException("Save the portable backup outside rekordbox's settings folder.");
        var data = JsonSerializer.SerializeToUtf8Bytes(profile, ArchiveJson.ProfileInfo);
        if (data.Length > SettingsPolicy.MaxArchiveSize) throw new SetupException("Backup exceeds the size limit.");
        AtomicWrite(path, data);
    }

    public ImportPlan Plan(SetupProfile profile, string destination, HashSet<SettingsGroup> groups)
    {
        RequireRekordboxClosed();
        Validate(profile);
        var directory = Root(destination);
        var main = MainSettings(directory);
        var targetVersion = Version(main);
        var edits = new List<FileEdit>();
        var changes = new List<Change>();
        var skipped = new List<string>();
        var observed = new Dictionary<string, byte[]?> { [SettingsPolicy.MainFile] = main };
        foreach (var file in profile.Properties.Keys.Union(profile.StructuredPreferences.Keys).Order(StringComparer.Ordinal))
        {
            // Version 1 sampler properties are intentionally remapped to the current sampler group.
            var incoming = (profile.Properties.GetValueOrDefault(file) ?? []).Where(kv => SettingsPolicy.Group(kv.Key, file) is { } g && groups.Contains(g)).ToDictionary();
            var fragments = (profile.StructuredPreferences.GetValueOrDefault(file) ?? []).Where(kv => SettingsPolicy.StructureGroup(kv.Key, file) is { } g && groups.Contains(g)).ToDictionary();
            if (incoming.Count == 0 && fragments.Count == 0) continue;
            var before = Read(file, directory);
            observed[file] = before;
            if (before == null) { skipped.Add($"{file}: not present on this computer"); continue; }
            Dictionary<string, string> current;
            Dictionary<string, System.Xml.Linq.XElement> elements;
            try
            {
                current = SettingsXml.Scalars(before, file);
                elements = SettingsXml.Elements(SettingsXml.Parse(before));
            }
            catch (Exception error) when (error is SetupException or System.Xml.XmlException)
            {
                skipped.Add($"{file}: destination preference structure is unsupported");
                continue;
            }
            var updates = new Dictionary<string, string>();
            foreach (var key in incoming.Keys.Order(StringComparer.Ordinal))
            {
                if (!current.TryGetValue(key, out var old)) { skipped.Add($"{key}: not present on this computer"); continue; }
                if (old == incoming[key]) continue;
                updates[key] = incoming[key];
                changes.Add(new Change(SettingsPolicy.Group(key, file)!.Value, key, $"{old} → {incoming[key]}"));
            }
            var structureUpdates = new Dictionary<string, byte[]>();
            foreach (var key in fragments.Keys.Order(StringComparer.Ordinal))
            {
                if (!elements.TryGetValue(key, out var original)) { skipped.Add($"{key}: not present on this computer"); continue; }
                var replacement = SettingsXml.ValidatedStructure(fragments[key], key, file);
                if (!SettingsXml.CompatibleStructure(original, replacement, key, file)) { skipped.Add($"{key}: display/effect fields differ on this computer"); continue; }
                if (SettingsXml.StructureEqual(original, replacement)) continue;
                structureUpdates[key] = fragments[key];
                changes.Add(new Change(SettingsPolicy.StructureGroup(key, file)!.Value, key,
                    file == SettingsPolicy.BrowserFile ? "Update browser display (order, visibility, sizing or sorting)" : "Update Groove Circuit effect beats"));
            }
            if (updates.Count > 0 || structureUpdates.Count > 0) edits.Add(new FileEdit(file, before, SettingsXml.Merge(updates, structureUpdates, before, file)));
        }
        var padCompatible = false;
        if (groups.Contains(SettingsGroup.mappings) && profile.Files.Keys.Any(k => k.StartsWith("pad/", StringComparison.Ordinal)))
        {
            observed["pad/Version"] = Read("pad/Version", directory);
            padCompatible = observed["pad/Version"] != null && Same(profile.Files.GetValueOrDefault("pad/Version"), observed["pad/Version"]);
        }
        foreach (var path in profile.Files.Keys.Order(StringComparer.Ordinal))
        {
            var group = SettingsPolicy.FileGroup(path)!.Value;
            if (!groups.Contains(group)) continue;
            if (path.StartsWith("pad/", StringComparison.Ordinal) && !padCompatible) { skipped.Add($"{path}: pad format version differs or is unknown"); continue; }
            var before = Read(path, directory);
            observed[path] = before;
            if (group == SettingsGroup.effects && before == null) { skipped.Add($"{path}: not present on this computer"); continue; }
            var after = profile.Files[path];
            if (Same(before, after)) continue;
            if (before != null && (path.EndsWith(".xml", StringComparison.Ordinal) || path.EndsWith(".mappings", StringComparison.Ordinal)) &&
                !SettingsXml.CompatibleFile(before, after))
            {
                skipped.Add($"{path}: XML file format differs or is unsupported on this computer");
                continue;
            }
            edits.Add(new FileEdit(path, before, after));
            changes.Add(new Change(group, path, before == null ? "Add mapping file" : "Replace settings file"));
        }
        return new ImportPlan(directory, profile, targetVersion, edits, changes, skipped, observed);
    }

    public PadFXCopyPlan PlanPadFXCopy(string destination, int sourceDeck, int targetDeck, int[] banks)
    {
        RequireRekordboxClosed();
        var directory = Root(destination);
        var main = MainSettings(directory);
        var version = Version(main);
        var before = Read(PadFXSettings.Filename, directory) ?? throw new SetupException("Missing PadFxSettings.xml.");
        var copy = PadFXSettings.Copy(before, sourceDeck, targetDeck, banks);
        return new PadFXCopyPlan(directory, sourceDeck, targetDeck, banks.ToArray(), version, main,
            copy.Changes.Count == 0 ? [] : [new FileEdit(PadFXSettings.Filename, before, copy.Data)], copy.Changes);
    }

    private static void ProtectDirectory(string path)
    {
        var user = WindowsIdentity.GetCurrent().User ?? throw new SetupException("Cannot determine the current Windows user.");
        var security = new DirectorySecurity();
        security.SetOwner(user);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    private string WriteRecovery(List<FileEdit> edits, string destination, string name)
    {
        RejectReparsePoints(RecoveryDirectory);
        if (Within(RecoveryDirectory, destination) || Within(RecoveryDirectory, PioneerDirectory))
            throw new SetupException("Restore points must be stored outside rekordbox's settings folder.");
        Directory.CreateDirectory(RecoveryDirectory);
        RejectReparsePoints(RecoveryDirectory);
        ProtectDirectory(RecoveryDirectory);
        var archive = new RecoveryArchive("rekordbox-local-recovery", 1, DateTimeOffset.UtcNow, destination, name,
            edits.Select(e => new RecoveryEntry(e.Path, e.Before, Digest(e.After))).ToArray());
        var data = JsonSerializer.SerializeToUtf8Bytes(archive, ArchiveJson.RecoveryInfo);
        if (data.Length > SettingsPolicy.MaxArchiveSize) throw new SetupException("Restore point exceeds the size limit.");
        var path = Path.Combine(RecoveryDirectory, $"{archive.CreatedAt:yyyy-MM-ddTHH-mm-ssZ}-{Guid.NewGuid().ToString("N")[..8]}.rbrecovery");
        // The file inherits the protected current-user-only ACL from the directory at creation.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough);
        stream.Write(data);
        stream.Flush(true);
        return path;
    }

    private static void AtomicWrite(string path, byte[] data)
    {
        RejectReparsePoints(path);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".rbcloner-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                stream.Write(data);
                stream.Flush(true);
            }
            RejectReparsePoints(path);
            if (File.Exists(path)) File.Replace(temporary, path, null); // Preserve the destination ACL.
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Write(byte[]? data, string relative, string directory)
    {
        var path = FilePath(relative, directory);
        if (data != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _ = FilePath(relative, directory);
            AtomicWrite(path, data);
        }
        else if (File.Exists(path)) File.Delete(path);
    }

    private string Commit(List<FileEdit> edits, string destination, string name)
    {
        RequireRekordboxClosed();
        destination = Root(destination);
        if (edits.Count == 0) throw new SetupException("There are no changes to apply.");
        if (edits.Count > SettingsPolicy.MaxFiles + SettingsPolicy.PreferenceFiles.Length || edits.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != edits.Count)
            throw new SetupException("Invalid or duplicate edits.");
        foreach (var edit in edits)
        {
            if (!SettingsPolicy.PreferenceFiles.Contains(edit.Path) && SettingsPolicy.FileGroup(edit.Path) == null) throw new SetupException($"Unapproved edit: {edit.Path}");
            if (!Same(Read(edit.Path, destination), edit.Before)) throw new SetupException("Settings changed since the preview. Create a fresh preview before applying changes.");
        }
        var recovery = WriteRecovery(edits, destination, name);
        var attempted = new List<FileEdit>();
        try
        {
            RequireRekordboxClosed();
            foreach (var edit in edits)
            {
                attempted.Add(edit);
                Write(edit.After, edit.Path, destination);
            }
        }
        catch (Exception error)
        {
            var failures = new List<string>();
            foreach (var edit in attempted.AsEnumerable().Reverse())
                try
                {
                    if (!Same(Read(edit.Path, destination), edit.Before)) Write(edit.Before, edit.Path, destination);
                }
                catch (Exception) { failures.Add(edit.Path); }
            var result = failures.Count == 0 ? "The original files were restored." : $"Some files could not be restored: {string.Join(", ", failures)}. Use the saved restore point.";
            throw new SetupException($"Settings update failed: {error.Message}\n{result}\nRestore point: {recovery}");
        }
        return recovery;
    }

    public string Apply(ImportPlan plan)
    {
        foreach (var (path, data) in plan.Observed)
            if (!Same(Read(path, plan.Destination), data)) throw new SetupException("Settings changed since the preview. Create a fresh preview before importing.");
        return Commit(plan.Edits, plan.Destination, plan.Profile.Name);
    }

    public string Apply(PadFXCopyPlan plan)
    {
        if (!Same(Read(SettingsPolicy.MainFile, plan.Destination), plan.MainSettings)) throw new SetupException("Settings changed since the preview. Create a fresh preview before copying Pad FX.");
        return Commit(plan.Edits, plan.Destination, $"Pad FX copy: Deck {plan.SourceDeck} → Deck {plan.TargetDeck}");
    }

    public RecoveryArchive LoadRecovery(string path, string destination)
    {
        var archive = LoadArchive(path, ArchiveJson.RecoveryInfo);
        var directory = Root(destination);
        if (archive.Format != "rekordbox-local-recovery" || archive.SchemaVersion != 1 || archive.Destination != directory ||
            archive.Entries == null || archive.Entries.Length == 0 || archive.Entries.Length > SettingsPolicy.MaxFiles + SettingsPolicy.PreferenceFiles.Length ||
            archive.Entries.Any(e => e == null) || archive.Entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Length)
            throw new SetupException("This restore point is invalid or belongs to a different settings folder. Select the folder on the computer where it was created.");
        foreach (var entry in archive.Entries)
        {
            if (!SettingsPolicy.PreferenceFiles.Contains(entry.Path) && SettingsPolicy.FileGroup(entry.Path) == null) throw new SetupException($"Unapproved restore path: {entry.Path}");
            if (entry.Path == SettingsPolicy.MainFile && entry.Original == null) throw new SetupException("Invalid main settings restore entry.");
            if (entry.AppliedSHA256 != null && !Regex.IsMatch(entry.AppliedSHA256, "^[0-9a-f]{64}$")) throw new SetupException("Invalid restore-point hash.");
            if (entry.Original is { } data)
                if (SettingsPolicy.PreferenceFiles.Contains(entry.Path)) _ = SettingsXml.Parse(data);
                else ValidateAuxiliary(data, entry.Path);
        }
        return archive;
    }

    public RecoveryPreview RecoveryEdits(RecoveryArchive archive, string destination)
    {
        RequireRekordboxClosed();
        var directory = Root(destination);
        if (archive.Destination != directory) throw new SetupException("Restore point belongs to a different folder.");
        var edits = new List<FileEdit>();
        var changed = new List<string>();
        foreach (var entry in archive.Entries)
        {
            var current = Read(entry.Path, directory);
            if (Same(current, entry.Original)) continue;
            if (Digest(current) != entry.AppliedSHA256) changed.Add(entry.Path);
            edits.Add(new FileEdit(entry.Path, current, entry.Original));
        }
        return new RecoveryPreview(edits, changed);
    }

    public string Restore(List<FileEdit> edits, string destination) => Commit(edits, destination, "Before restoring previous setup");
}
