using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RekordboxSetupCloner;

public static class SettingsXml
{
    public static XDocument Parse(byte[] data)
    {
        string text;
        try { text = new UTF8Encoding(false, true).GetString(data); }
        catch (DecoderFallbackException) { throw new SetupException("Unsupported settings XML encoding."); }
        if (data.Length > SettingsPolicy.MaxFileSize || text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || text.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase))
            throw new SetupException("Unsupported settings XML (size or document type).");
        using var stream = new MemoryStream(data);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = SettingsPolicy.MaxFileSize
        });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name != "PROPERTIES") throw new SetupException("Expected a rekordbox PROPERTIES document.");
        return document;
    }

    public static byte[] Bytes(XDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, NewLineHandling = NewLineHandling.None }))
            document.Save(writer);
        return stream.ToArray();
    }

    public static Dictionary<string, XElement> Elements(XDocument document)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var element in document.Root!.Elements("VALUE"))
        {
            if ((string?)element.Attribute("name") is not { } name) continue;
            if (!result.TryAdd(name, element)) throw new SetupException($"Duplicate settings property: {name}");
        }
        return result;
    }

    private static Dictionary<string, XAttribute> ScalarAttributes(XDocument document, string file)
    {
        var elements = Elements(document);
        var result = new Dictionary<string, XAttribute>(StringComparer.Ordinal);
        foreach (var (name, element) in elements)
            if (element.Attribute("val") is { } attribute && !element.HasElements) result[name] = attribute;
        if (file == SettingsPolicy.SamplerFile && elements.TryGetValue("SamplerSet", out var sampler))
        {
            var containers = sampler.Elements("SamplerSet").ToArray();
            if (containers.Length != 1) throw new SetupException("Unsupported sampler settings container.");
            foreach (var element in containers[0].Elements("VALUE"))
            {
                if ((string?)element.Attribute("name") is not { } name || !SettingsPolicy.SamplerKeys.Contains(name) ||
                    element.Attribute("val") is not { } attr || element.HasElements) continue;
                if (!result.TryAdd($"SamplerSet/{name}", attr)) throw new SetupException($"Duplicate sampler preference: {name}");
            }
        }
        if (file == SettingsPolicy.VideoFile)
            foreach (var name in new[] { "ImageOverlay", "TextOverlay" })
            {
                if (!elements.TryGetValue(name, out var property)) continue;
                var containers = property.Elements(name).ToArray();
                if (containers.Length != 1) throw new SetupException("Unsupported video overlay settings container.");
                foreach (var setting in new[] { "transparency", "size" })
                {
                    var children = containers[0].Elements(setting).ToArray();
                    if (children.Length > 1) throw new SetupException("Duplicate video overlay preference.");
                    if (children.FirstOrDefault() is { } child && child.Attribute("value") is { } attr && !child.HasElements)
                        result[$"{name}/{setting}"] = attr;
                }
            }
        return result;
    }

    public static Dictionary<string, string> Scalars(byte[] data, string file = SettingsPolicy.MainFile) =>
        ScalarAttributes(Parse(data), file).ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.Ordinal);

    public static byte[] Merge(Dictionary<string, string> values, Dictionary<string, byte[]> structures, byte[] data, string file)
    {
        var document = Parse(data);
        var elements = Elements(document);
        var attributes = ScalarAttributes(document, file);
        foreach (var (key, value) in values)
        {
            if (!attributes.TryGetValue(key, out var attribute)) throw new SetupException($"The destination has no scalar setting named {key}.");
            attribute.Value = value;
        }
        foreach (var (key, fragment) in structures)
        {
            if (!elements.TryGetValue(key, out var original)) throw new SetupException($"The destination has no setting named {key}.");
            var replacement = ValidatedStructure(fragment, key, file);
            if (!CompatibleStructure(original, replacement, key, file)) throw new SetupException($"Incompatible display/effect preference: {key}");
            original.ReplaceWith(new XElement(replacement));
        }
        return Bytes(document);
    }

    public static Dictionary<string, byte[]> Structures(byte[] data, string file)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (key, element) in Elements(Parse(data)))
        {
            if (SettingsPolicy.StructureGroup(key, file) == null) continue;
            if ((string?)element.Attribute("val") == "" && !element.HasElements) continue;
            ValidateStructureElement(element, key, file);
            result[key] = Bytes(new XDocument(new XElement("PROPERTIES", new XElement(element))));
        }
        return result;
    }

    public static XElement ValidatedStructure(byte[] data, string key, string file)
    {
        var root = Parse(data).Root!;
        if (root.HasAttributes || root.Elements().Count() != 1) throw new SetupException($"Invalid preference fragment: {key}");
        var element = root.Elements().Single();
        ValidateStructureElement(element, key, file);
        return new XElement(element);
    }

    private static void ValidateStructureElement(XElement element, string key, string file)
    {
        if (SettingsPolicy.StructureGroup(key, file) == null || element.Name != "VALUE" || (string?)element.Attribute("name") != key ||
            !element.Attributes().Select(a => a.Name.ToString()).ToHashSet().SetEquals(["name"]) || element.Elements().Count() != 1)
            throw new SetupException($"Unsupported preference structure: {key}");
        SetupException Fail() => new($"Unsupported display/effect fields in {key}; no library references can be copied here.");
        void Numeric(XElement node, string name, string[] required, string[]? optional = null)
        {
            var names = node.Attributes().Select(a => a.Name.ToString()).ToHashSet();
            if (node.Name != name || !names.IsSupersetOf(required) || !names.IsSubsetOf(required.Concat(optional ?? []))) throw Fail();
            foreach (var attr in node.Attributes())
                if (attr.Value.Length > 32 || !double.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw Fail();
        }
        void WhitespaceOnly(XElement node)
        {
            foreach (var child in node.Nodes())
                if (child is XElement e) WhitespaceOnly(e);
                else if (child is not XText t || child is XCData || !string.IsNullOrWhiteSpace(t.Value)) throw Fail();
        }
        WhitespaceOnly(element);
        var body = element.Elements().Single();
        var children = body.Elements().ToArray();
        if (file == SettingsPolicy.BrowserFile)
        {
            if (key.StartsWith("ArtworkStatus-", StringComparison.Ordinal))
            {
                Numeric(body, "ARTWORKSTATUS", []);
                if (children.Length != 1) throw Fail();
                Numeric(children[0], "STATUS", ["mode", "size"]);
            }
            else
            {
                if (key.EndsWith("-AttributeColumn", StringComparison.Ordinal)) Numeric(body, "ATTRIBUTE_COLUMN", []);
                else Numeric(body, "TABLELAYOUT", ["sortedCol", "sortForwards"], ["hybridCol"]);
                if (children.Length is < 1 or > 256) throw Fail();
                var ids = new HashSet<string>();
                foreach (var column in children)
                {
                    Numeric(column, "COLUMN", ["id", "visible", "width"]);
                    if (!ids.Add(column.Attribute("id")!.Value)) throw Fail();
                }
            }
        }
        else
        {
            Numeric(body, key, []);
            if (children.Length is < 1 or > 64) throw Fail();
            var indices = new HashSet<string>();
            foreach (var beat in children)
            {
                Numeric(beat, "Beat", ["idx", "numerator", "denominator"]);
                if (!indices.Add(beat.Attribute("idx")!.Value)) throw Fail();
            }
        }
        if (children.Any(c => c.HasElements)) throw Fail();
    }

    public static bool CompatibleStructure(XElement current, XElement incoming, string key, string file)
    {
        try { ValidateStructureElement(current, key, file); }
        catch (SetupException) { return false; }
        ValidateStructureElement(incoming, key, file);
        var a = current.Elements().Single();
        var b = incoming.Elements().Single();
        static HashSet<string> Identities(XElement node) => node.Elements().Select(e => $"{e.Name}:{(string?)e.Attribute("id") ?? (string?)e.Attribute("idx") ?? ""}").ToHashSet();
        return a.Name == b.Name && Identities(a).SetEquals(Identities(b)) &&
            a.Attributes().Select(x => x.Name).ToHashSet().SetEquals(b.Attributes().Select(x => x.Name));
    }

    public static bool StructureEqual(XElement a, XElement b) => a.Name == b.Name &&
        a.Attributes().ToDictionary(x => x.Name, x => x.Value).OrderBy(k => k.Key.ToString(), StringComparer.Ordinal)
            .SequenceEqual(b.Attributes().ToDictionary(x => x.Name, x => x.Value).OrderBy(k => k.Key.ToString(), StringComparer.Ordinal)) &&
        a.Elements().Count() == b.Elements().Count() && a.Elements().Zip(b.Elements()).All(pair => StructureEqual(pair.First, pair.Second));

    // Whole XML files must use the same records/fields before replacing a counterpart.
    // Values can differ; named properties, slot identities and explicit format versions cannot.
    public static bool CompatibleFile(byte[] current, byte[] incoming)
    {
        XDocument a, b;
        try
        {
            a = Parse(current);
            b = Parse(incoming);
            _ = Elements(a);
            _ = Elements(b);
        }
        catch (Exception error) when (error is SetupException or XmlException) { return false; }

        static bool FormatField(string name) => new[] { "version", "formatVersion", "schemaVersion" }.Contains(name, StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, string> Identities(XElement node) => node.Attributes()
            .Where(attr => (node.Name == "VALUE" && attr.Name == "name") ||
                new[] { "deckNo", "modeIndex", "padIndex", "idx", "index", "unitNo" }.Contains(attr.Name.ToString()) || FormatField(attr.Name.ToString()))
            .ToDictionary(attr => attr.Name.ToString(), attr => attr.Value);
        static string Key(XElement node, int position)
        {
            var identities = Identities(node);
            return node.Name + ":" + (identities.Count == 0 ? position.ToString(CultureInfo.InvariantCulture) :
                string.Join(";", identities.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value.Length}:{kv.Value}")));
        }
        static bool Schema(XElement x, XElement y, int depth)
        {
            if (depth > 64 || x.Name != y.Name || !x.Attributes().Select(attr => attr.Name).ToHashSet().SetEquals(y.Attributes().Select(attr => attr.Name))) return false;
            var ids = Identities(x);
            if (!ids.OrderBy(kv => kv.Key, StringComparer.Ordinal).SequenceEqual(Identities(y).OrderBy(kv => kv.Key, StringComparer.Ordinal))) return false;
            if (x.Name == "VALUE" && FormatField((string?)x.Attribute("name") ?? "") && (string?)x.Attribute("val") != (string?)y.Attribute("val")) return false;
            var left = x.Elements().Select((node, index) => (Key: Key(node, index), Node: node)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
            var right = y.Elements().Select((node, index) => (Key: Key(node, index), Node: node)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
            return left.Length == right.Length && left.Select(item => item.Key).Distinct().Count() == left.Length &&
                left.Zip(right).All(pair => pair.First.Key == pair.Second.Key && Schema(pair.First.Node, pair.Second.Node, depth + 1));
        }
        return Schema(a.Root!, b.Root!, 0);
    }
}
