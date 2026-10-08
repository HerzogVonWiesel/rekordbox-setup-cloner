using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RekordboxSetupCloner;

public sealed class SetupException(string message) : Exception(message);

public enum SettingsGroup { stems, waveforms, layout, performance, sampler, groove, effects, mappings, analysis, recording, controller, visuals }

public static class GroupInfo
{
    public static readonly SettingsGroup[] All = Enum.GetValues<SettingsGroup>();
    public static HashSet<SettingsGroup> DefaultSelection => All.Where(g => g != SettingsGroup.visuals).ToHashSet();
    public static string Title(this SettingsGroup group) => group switch
    {
        SettingsGroup.stems => "STEMS",
        SettingsGroup.waveforms => "Waveforms",
        SettingsGroup.layout => "Screen layout",
        SettingsGroup.performance => "Deck & mixer preferences",
        SettingsGroup.sampler => "Sampler & sequencer",
        SettingsGroup.groove => "Groove Circuit",
        SettingsGroup.effects => "Pad FX & effects",
        SettingsGroup.mappings => "Keyboard, MIDI & pad mappings",
        SettingsGroup.analysis => "Analysis preferences",
        SettingsGroup.recording => "Recording preferences",
        SettingsGroup.controller => "Controller feel & display",
        SettingsGroup.visuals => "Video & lighting",
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };
    public static string Detail(this SettingsGroup group) => group switch
    {
        SettingsGroup.stems => "Activation, speed/quality, memory, multithreading and part layout",
        SettingsGroup.waveforms => "Colours, zoom, beat grid and vocal display",
        SettingsGroup.layout => "Decks, panels, browser columns, fonts and source visibility",
        SettingsGroup.performance => "Deck behaviour, quantize, Auto Mix, Mix Point Link and faders",
        SettingsGroup.sampler => "Layout, banks, tempo, sync, quantize and capture behaviour; no samples",
        SettingsGroup.groove => "Activation, drum capture behaviour and effect preferences",
        SettingsGroup.effects => "Pad FX banks, effect units and Merge FX",
        SettingsGroup.mappings => "Custom shortcuts, controller mappings and pad editor layouts",
        SettingsGroup.analysis => "BPM, key, phrase, vocal and cue-analysis options; no analysis data",
        SettingsGroup.recording => "Triggers, silence detection, splitting and normalization; no paths",
        SettingsGroup.controller => "Jog displays, LEDs, slip indicators, backspin and fader start",
        SettingsGroup.visuals => "Video effects, overlays and lighting behaviour; no media or fixtures",
        _ => ""
    };
}

public sealed record RekordboxVersion(string Raw)
{
    [JsonIgnore] public int[]? Components
    {
        get
        {
            if (Raw.Length == 5 && int.TryParse(Raw, out var n) && n is >= 60000 and < 100000)
                return [n / 10000, n / 100 % 100, n % 100];
            var pieces = Raw.Split('.');
            if (pieces.Length < 3) return null;
            var result = new int[3];
            for (var i = 0; i < 3; i++) if (!int.TryParse(pieces[i], out result[i]) || result[i] < 0) return null;
            return result;
        }
    }
    [JsonIgnore] public int? Major => Components?.FirstOrDefault();
    [JsonIgnore] public string Display => Components is { } parts ? string.Join('.', parts) : "Unknown";
    [JsonIgnore] public string Label => Components != null ? $"rekordbox {Display}" : "rekordbox version unavailable";

    public string? CompatibilityNotice(RekordboxVersion destination)
    {
        if (Components == null || destination.Components == null)
            return "The rekordbox version is unavailable for the backup or destination. Matching preferences can still be imported; missing settings and incompatible file formats are skipped.";
        if (Components.SequenceEqual(destination.Components)) return null;
        return $"The backup uses rekordbox {Display}; the destination uses {destination.Display}. Versions differ, so only matching preferences and compatible file formats will be transferred.";
    }
}

// Property names, ISO-8601 dates and base64 byte arrays match Swift's existing archive format.
public sealed record SetupProfile(string Format, int SchemaVersion, DateTimeOffset CreatedAt, string Name,
    RekordboxVersion RekordboxVersion, SettingsGroup[] Groups, Dictionary<string, Dictionary<string, string>> Properties,
    Dictionary<string, byte[]> Files, Dictionary<string, Dictionary<string, byte[]>>? Structures = null)
{
    [JsonIgnore] public Dictionary<string, Dictionary<string, byte[]>> StructuredPreferences => Structures ?? [];
    [JsonIgnore] public int PreferenceCount => Properties.Values.Sum(v => v.Count) + StructuredPreferences.Values.Sum(v => v.Count);
}

public sealed record Change(SettingsGroup Group, string Title, string Detail);
public sealed record FileEdit(string Path, byte[]? Before, byte[]? After);
public sealed record ImportPlan(string Destination, SetupProfile Profile, RekordboxVersion TargetVersion,
    List<FileEdit> Edits, List<Change> Changes, List<string> Skipped, Dictionary<string, byte[]?> Observed)
{
    public string? CompatibilityNotice => Profile.RekordboxVersion.CompatibilityNotice(TargetVersion);
}
public sealed record PadFXCopyPlan(string Destination, int SourceDeck, int TargetDeck, int[] Banks,
    RekordboxVersion Version, byte[] MainSettings, List<FileEdit> Edits, List<Change> Changes);
public sealed record RecoveryEntry(string Path, byte[]? Original, string? AppliedSHA256);
public sealed record RecoveryArchive(string Format, int SchemaVersion, DateTimeOffset CreatedAt,
    string Destination, string ProfileName, RecoveryEntry[] Entries);
public sealed record RecoveryPreview(List<FileEdit> Edits, List<string> Changed);
public sealed record SettingsInspection(RekordboxVersion Version, Dictionary<SettingsGroup, int> Counts, bool? StemsEnabled);

public static class ArchiveJson
{
    public static JsonTypeInfo<SetupProfile> ProfileInfo => ArchiveJsonContext.Default.SetupProfile;
    public static JsonTypeInfo<RecoveryArchive> RecoveryInfo => ArchiveJsonContext.Default.RecoveryArchive;

    // Swift's .iso8601 date decoder expects whole seconds, without fractional seconds.
    internal sealed class IsoDateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDateTimeOffset();
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
    }

    internal sealed class GroupConverter() : JsonStringEnumConverter<SettingsGroup>(allowIntegerValues: false);
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, MaxDepth = 32,
    Converters = [typeof(ArchiveJson.IsoDateConverter), typeof(ArchiveJson.GroupConverter)])]
[JsonSerializable(typeof(SetupProfile))]
[JsonSerializable(typeof(RecoveryArchive))]
internal partial class ArchiveJsonContext : JsonSerializerContext;
