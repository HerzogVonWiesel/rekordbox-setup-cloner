import Foundation

enum PadFXCopyDirection: String, CaseIterable, Identifiable {
    case deck1To2, deck2To1

    var id: String { rawValue }
    var sourceDeck: Int { self == .deck1To2 ? 1 : 2 }
    var targetDeck: Int { self == .deck1To2 ? 2 : 1 }
    var title: String { "Deck \(sourceDeck) → Deck \(targetDeck)" }
}

enum PadFXBanks: String, CaseIterable, Identifiable {
    case both, fx1, fx2

    var id: String { rawValue }
    var title: String {
        switch self {
        case .both: return "Both banks"
        case .fx1: return "Pad FX 1"
        case .fx2: return "Pad FX 2"
        }
    }
    var modeIndices: [Int] {
        switch self {
        case .both: return [0, 1]
        case .fx1: return [0]
        case .fx2: return [1]
        }
    }
}

enum PadFXSettings {
    static let filename = "PadFxSettings.xml"

    /// The inspected PADFXINFO_500 format uses one-based deck numbers and zero-based banks/pads.
    /// Build a new document in memory; the store handles preview, recovery and writes separately.
    static func copying(_ data: Data, direction: PadFXCopyDirection, banks: PadFXBanks) throws -> (data: Data, changes: [Change]) {
        let document = try SettingsXML.parse(data)
        let values = try SettingsXML.elements(document)
        guard let value = values["PadFXSettings"],
              value.elements(forName: "PadFXSettings").count == 1,
              let container = value.elements(forName: "PadFXSettings").first else {
            throw SetupError("This file does not have a recognised Pad FX settings container.")
        }
        let entries = (container.children ?? []).compactMap { $0 as? XMLElement }
        // Validate the schema before interpreting any deck or slot identities.
        for entry in entries {
            guard entry.name == "PADFXINFO_500",
                  let deck = Int(entry.attribute(forName: "deckNo")?.stringValue ?? ""), (1...4).contains(deck),
                  let mode = Int(entry.attribute(forName: "modeIndex")?.stringValue ?? ""), (0...1).contains(mode),
                  let pad = Int(entry.attribute(forName: "padIndex")?.stringValue ?? ""), (0...15).contains(pad),
                  !(entry.children ?? []).contains(where: { $0.kind == .element }) else {
                throw SetupError("This Pad FX file uses an unsupported record format. No changes have been made.")
            }
        }

        func slots(deck: Int, mode: Int) throws -> [Int: XMLElement] {
            var result: [Int: XMLElement] = [:]
            for entry in entries where entry.attribute(forName: "deckNo")?.stringValue == String(deck) &&
                entry.attribute(forName: "modeIndex")?.stringValue == String(mode) {
                let pad = Int(entry.attribute(forName: "padIndex")!.stringValue!)!
                guard result[pad] == nil else { throw SetupError("Duplicate Pad FX slot on deck \(deck), bank \(mode + 1).") }
                result[pad] = entry
            }
            guard Set(result.keys) == Set(0..<16) else {
                throw SetupError("Deck \(deck), Pad FX \(mode + 1) does not contain the expected 16 saved pad slots.")
            }
            return result
        }

        func attributes(_ element: XMLElement) -> [String: String] {
            var result: [String: String] = [:]
            for attribute in element.attributes ?? [] {
                if let name = attribute.name { result[name] = attribute.stringValue ?? "" }
            }
            return result
        }

        var changes: [Change] = []
        for mode in banks.modeIndices {
            let source = try slots(deck: direction.sourceDeck, mode: mode)
            let target = try slots(deck: direction.targetDeck, mode: mode)
            for pad in 0..<16 {
                let original = target[pad]!
                let replacement = source[pad]!.copy() as! XMLElement
                replacement.attribute(forName: "deckNo")?.stringValue = String(direction.targetDeck)
                let before = attributes(original)
                let after = attributes(replacement)
                let changedKeys = Set(before.keys).union(after.keys).filter { before[$0] != after[$0] }.sorted()
                guard !changedKeys.isEmpty else { continue }
                let detail = changedKeys.map { "\($0): \(before[$0] ?? "unset") → \(after[$0] ?? "unset")" }.joined(separator: "; ")
                changes.append(Change(id: "\(mode)/\(pad)", group: .effects,
                                      title: "Pad FX \(mode + 1) · Pad \(pad + 1)", detail: detail))
                container.replaceChild(at: original.index, with: replacement)
            }
        }
        return (changes.isEmpty ? data : document.xmlData, changes)
    }
}
