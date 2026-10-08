using System.Xml.Linq;

namespace RekordboxSetupCloner;

public static class PadFXSettings
{
    public const string Filename = "PadFxSettings.xml";

    public static (byte[] Data, List<Change> Changes) Copy(byte[] data, int sourceDeck, int targetDeck, int[] banks)
    {
        if (!((sourceDeck == 1 && targetDeck == 2) || (sourceDeck == 2 && targetDeck == 1)) ||
            banks.Length == 0 || banks.Distinct().Count() != banks.Length || banks.Any(b => b is < 0 or > 1))
            throw new SetupException("Choose decks 1 and 2 and at least one valid Pad FX bank.");
        var document = SettingsXml.Parse(data);
        var values = SettingsXml.Elements(document);
        if (!values.TryGetValue("PadFXSettings", out var value) || value.Elements("PadFXSettings").Count() != 1)
            throw new SetupException("This file does not have a recognised Pad FX settings container.");
        var container = value.Element("PadFXSettings")!;
        var entries = container.Elements().ToArray();
        foreach (var entry in entries)
            if (entry.Name != "PADFXINFO_500" || !int.TryParse((string?)entry.Attribute("deckNo"), out var deck) || deck is < 1 or > 4 ||
                !int.TryParse((string?)entry.Attribute("modeIndex"), out var mode) || mode is < 0 or > 1 ||
                !int.TryParse((string?)entry.Attribute("padIndex"), out var pad) || pad is < 0 or > 15 || entry.HasElements)
                throw new SetupException("This Pad FX file uses an unsupported record format.");
        Dictionary<int, XElement> Slots(int deck, int mode)
        {
            var result = new Dictionary<int, XElement>();
            foreach (var entry in entries.Where(e => (string?)e.Attribute("deckNo") == deck.ToString() && (string?)e.Attribute("modeIndex") == mode.ToString()))
                if (!result.TryAdd(int.Parse(entry.Attribute("padIndex")!.Value), entry))
                    throw new SetupException($"Duplicate Pad FX slot on deck {deck}, bank {mode + 1}.");
            if (!result.Keys.ToHashSet().SetEquals(Enumerable.Range(0, 16)))
                throw new SetupException($"Deck {deck}, Pad FX {mode + 1} does not contain the expected 16 saved pad slots.");
            return result;
        }
        var changes = new List<Change>();
        foreach (var mode in banks)
        {
            var source = Slots(sourceDeck, mode);
            var target = Slots(targetDeck, mode);
            for (var pad = 0; pad < 16; pad++)
            {
                var original = target[pad];
                var replacement = new XElement(source[pad]);
                replacement.SetAttributeValue("deckNo", targetDeck);
                var before = original.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value);
                var after = replacement.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value);
                var keys = before.Keys.Union(after.Keys).Where(k => before.GetValueOrDefault(k) != after.GetValueOrDefault(k)).Order(StringComparer.Ordinal).ToArray();
                if (keys.Length == 0) continue;
                changes.Add(new Change(SettingsGroup.effects, $"Pad FX {mode + 1} · Pad {pad + 1}",
                    string.Join("; ", keys.Select(k => $"{k}: {before.GetValueOrDefault(k, "unset")} → {after.GetValueOrDefault(k, "unset")}"))));
                original.ReplaceWith(replacement);
            }
        }
        return (changes.Count == 0 ? data : SettingsXml.Bytes(document), changes);
    }
}
