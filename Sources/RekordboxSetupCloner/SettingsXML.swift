import Foundation

enum SettingsXML {
    static func parse(_ data: Data) throws -> XMLDocument {
        guard data.count <= SettingsPolicy.maxFileSize,
              let text = String(data: data, encoding: .utf8),
              !text.localizedCaseInsensitiveContains("<!DOCTYPE"),
              !text.localizedCaseInsensitiveContains("<!ENTITY") else {
            throw SetupError("Unsupported settings XML (encoding, size, or document type).")
        }
        let document = try XMLDocument(data: data, options: [.nodePreserveAll, .nodeLoadExternalEntitiesNever])
        guard document.rootElement()?.name == "PROPERTIES" else {
            throw SetupError("Expected a rekordbox PROPERTIES document.")
        }
        return document
    }

    static func elements(_ document: XMLDocument) throws -> [String: XMLElement] {
        var result: [String: XMLElement] = [:]
        for element in document.rootElement()?.elements(forName: "VALUE") ?? [] {
            guard let name = element.attribute(forName: "name")?.stringValue else { continue }
            guard result[name] == nil else { throw SetupError("Duplicate settings property: \(name)") }
            result[name] = element
        }
        return result
    }

    private static func scalarAttributes(_ document: XMLDocument, file: String) throws -> [String: XMLNode] {
        let elements = try elements(document)
        var result: [String: XMLNode] = [:]
        for (name, element) in elements {
            if let attribute = element.attribute(forName: "val"),
               !(element.children ?? []).contains(where: { $0.kind == .element }) {
                result[name] = attribute
            }
        }
        if file == SettingsPolicy.samplerFile, let property = elements["SamplerSet"] {
            let containers = property.elements(forName: "SamplerSet")
            guard containers.count == 1 else { throw SetupError("Unsupported sampler settings container.") }
            // Track elements contain library IDs and URLs. Only named control VALUEs are inspected.
            for element in containers[0].elements(forName: "VALUE") {
                guard let name = element.attribute(forName: "name")?.stringValue,
                      SettingsPolicy.samplerKeys.contains(name),
                      let attribute = element.attribute(forName: "val"),
                      !(element.children ?? []).contains(where: { $0.kind == .element }) else { continue }
                let key = "SamplerSet/\(name)"
                guard result[key] == nil else { throw SetupError("Duplicate sampler preference: \(name)") }
                result[key] = attribute
            }
        }
        if file == SettingsPolicy.videoFile {
            for name in ["ImageOverlay", "TextOverlay"] {
                guard let property = elements[name] else { continue }
                let containers = property.elements(forName: name)
                guard containers.count == 1 else { throw SetupError("Unsupported video overlay settings container.") }
                for setting in ["transparency", "size"] {
                    let children = containers[0].elements(forName: setting)
                    guard children.count <= 1 else { throw SetupError("Duplicate video overlay preference.") }
                    if let element = children.first, let attribute = element.attribute(forName: "value"),
                       !(element.children ?? []).contains(where: { $0.kind == .element }) {
                        result["\(name)/\(setting)"] = attribute
                    }
                }
            }
        }
        return result
    }

    static func scalars(_ data: Data, file: String = SettingsPolicy.mainFile) throws -> [String: String] {
        try scalarAttributes(parse(data), file: file).mapValues { $0.stringValue ?? "" }
    }

    static func merging(_ values: [String: String], structures: [String: Data] = [:], into data: Data, file: String) throws -> Data {
        let document = try parse(data)
        let existing = try elements(document)
        let attributes = try scalarAttributes(document, file: file)
        for (key, value) in values {
            guard let attribute = attributes[key] else {
                throw SetupError("The destination does not have a scalar setting named \(key).")
            }
            attribute.stringValue = value
        }
        for (key, fragment) in structures {
            guard let original = existing[key], let parent = original.parent as? XMLElement else {
                throw SetupError("The destination does not have a setting named \(key).")
            }
            let replacement = try validatedStructure(fragment, key: key, file: file)
            guard try compatibleStructure(original, replacement, key: key, file: file) else {
                throw SetupError("Incompatible display/effect preference: \(key)")
            }
            parent.replaceChild(at: original.index, with: replacement.copy() as! XMLElement)
        }
        return document.xmlData
    }

    static func structures(_ data: Data, file: String) throws -> [String: Data] {
        let properties = try elements(parse(data))
        var result: [String: Data] = [:]
        for (key, element) in properties where SettingsPolicy.structureGroup(for: key, in: file) != nil {
            // Some unused browser layouts are saved as empty scalar placeholders.
            if element.attribute(forName: "val")?.stringValue == "", element.elements.isEmpty { continue }
            try validateStructureElement(element, key: key, file: file)
            let wrapper = XMLElement(name: "PROPERTIES")
            wrapper.addChild(element.copy() as! XMLElement)
            result[key] = XMLDocument(rootElement: wrapper).xmlData
        }
        return result
    }

    static func validatedStructure(_ data: Data, key: String, file: String) throws -> XMLElement {
        let document = try parse(data)
        guard let root = document.rootElement(), root.attributes?.isEmpty ?? true,
              root.elements.count == 1, let element = root.elements.first else {
            throw SetupError("Invalid preference fragment: \(key)")
        }
        try validateStructureElement(element, key: key, file: file)
        // Detach so the returned node's lifetime is independent of the temporary document.
        element.detach()
        return element
    }

    private static func validateStructureElement(_ element: XMLElement, key: String, file: String) throws {
        guard SettingsPolicy.structureGroup(for: key, in: file) != nil,
              element.name == "VALUE", element.attribute(forName: "name")?.stringValue == key,
              Set((element.attributes ?? []).compactMap(\.name)) == ["name"],
              element.elements.count == 1, let body = element.elements.first else {
            throw SetupError("Unsupported preference structure: \(key)")
        }
        func fail() -> SetupError { SetupError("Unsupported display/effect fields in \(key); no library references can be copied here.") }
        func numeric(_ node: XMLElement, name: String, required: Set<String>, optional: Set<String> = []) throws {
            let attrs = node.attributes ?? []
            let names = Set(attrs.compactMap(\.name))
            guard node.name == name, required.isSubset(of: names), names.isSubset(of: required.union(optional)),
                  names.count == attrs.count else { throw fail() }
            for attribute in attrs {
                guard let text = attribute.stringValue, text.count <= 32, let number = Double(text), number.isFinite else { throw fail() }
            }
        }
        // Only schema-approved numeric fields can leave the source. Reject hidden text/comments too.
        func whitespaceOnly(_ node: XMLElement) throws {
            for child in node.children ?? [] {
                if child.kind == .element { try whitespaceOnly(child as! XMLElement) }
                else if child.kind != .text || !(child.stringValue ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { throw fail() }
            }
        }
        try whitespaceOnly(element)
        if file == SettingsPolicy.browserFile {
            if key.hasPrefix("ArtworkStatus-") {
                try numeric(body, name: "ARTWORKSTATUS", required: [])
                guard body.elements.count == 1 else { throw fail() }
                try numeric(body.elements[0], name: "STATUS", required: ["mode", "size"])
            } else {
                if key.hasSuffix("-AttributeColumn") {
                    try numeric(body, name: "ATTRIBUTE_COLUMN", required: [])
                } else {
                    try numeric(body, name: "TABLELAYOUT", required: ["sortedCol", "sortForwards"], optional: ["hybridCol"])
                }
                guard !body.elements.isEmpty, body.elements.count <= 256 else { throw fail() }
                var identifiers: Set<String> = []
                for column in body.elements {
                    try numeric(column, name: "COLUMN", required: ["id", "visible", "width"])
                    guard identifiers.insert(column.attribute(forName: "id")!.stringValue!).inserted else { throw fail() }
                }
            }
        } else {
            try numeric(body, name: key, required: [])
            guard !body.elements.isEmpty, body.elements.count <= 64 else { throw fail() }
            var indices: Set<String> = []
            for beat in body.elements {
                try numeric(beat, name: "Beat", required: ["idx", "numerator", "denominator"])
                guard indices.insert(beat.attribute(forName: "idx")!.stringValue!).inserted else { throw fail() }
            }
        }
        guard body.elements.allSatisfy({ $0.elements.isEmpty }) else { throw fail() }
    }

    static func compatibleStructure(_ current: XMLElement, _ incoming: XMLElement, key: String, file: String) throws -> Bool {
        // Do not replace a newer/different layout and lose columns or additional fields.
        do { try validateStructureElement(current, key: key, file: file) }
        catch { return false }
        try validateStructureElement(incoming, key: key, file: file)
        let currentBody = current.elements[0]
        let incomingBody = incoming.elements[0]
        func identities(_ node: XMLElement) -> Set<String> {
            Set(node.elements.map { "\($0.name ?? ""):\($0.attribute(forName: "id")?.stringValue ?? $0.attribute(forName: "idx")?.stringValue ?? "")" })
        }
        return currentBody.name == incomingBody.name && identities(currentBody) == identities(incomingBody) &&
            Set((currentBody.attributes ?? []).compactMap(\.name)) == Set((incomingBody.attributes ?? []).compactMap(\.name))
    }

    static func structureEqual(_ lhs: XMLElement, _ rhs: XMLElement) -> Bool {
        func attributes(_ node: XMLElement) -> [String: String] {
            var result: [String: String] = [:]
            for attribute in node.attributes ?? [] { if let name = attribute.name { result[name] = attribute.stringValue ?? "" } }
            return result
        }
        return lhs.name == rhs.name && attributes(lhs) == attributes(rhs) && lhs.elements.count == rhs.elements.count &&
            zip(lhs.elements, rhs.elements).allSatisfy { structureEqual($0.0, $0.1) }
    }

    /// Compare whole-file schemas, preserving destination-only fields and record identities.
    static func compatibleFile(_ current: Data, _ incoming: Data) -> Bool {
        guard let a = try? parse(current), let b = try? parse(incoming),
              let lhs = a.rootElement(), let rhs = b.rootElement(),
              (try? elements(a)) != nil, (try? elements(b)) != nil else { return false }
        func formatField(_ name: String) -> Bool {
            ["version", "formatversion", "schemaversion"].contains(name.lowercased())
        }
        func identities(_ node: XMLElement) -> [String: String] {
            var result: [String: String] = [:]
            for attr in node.attributes ?? [] {
                guard let name = attr.name else { continue }
                if (node.name == "VALUE" && name == "name") ||
                    ["deckNo", "modeIndex", "padIndex", "idx", "index", "unitNo"].contains(name) || formatField(name) {
                    result[name] = attr.stringValue ?? ""
                }
            }
            return result
        }
        func key(_ node: XMLElement, position: Int) -> String {
            let ids = identities(node)
            let identity = ids.isEmpty ? String(position) : ids.keys.sorted().map {
                "\($0)=\(ids[$0]!.count):\(ids[$0]!)"
            }.joined(separator: ";")
            return "\(node.name ?? ""):\(identity)"
        }
        func schema(_ x: XMLElement, _ y: XMLElement, depth: Int) -> Bool {
            guard depth <= 64, x.name == y.name,
                  Set((x.attributes ?? []).compactMap(\.name)) == Set((y.attributes ?? []).compactMap(\.name)),
                  identities(x) == identities(y) else { return false }
            if x.name == "VALUE", formatField(x.attribute(forName: "name")?.stringValue ?? ""),
               x.attribute(forName: "val")?.stringValue != y.attribute(forName: "val")?.stringValue { return false }
            let left = x.elements.enumerated().map { (key($0.element, position: $0.offset), $0.element) }.sorted { $0.0 < $1.0 }
            let right = y.elements.enumerated().map { (key($0.element, position: $0.offset), $0.element) }.sorted { $0.0 < $1.0 }
            return left.count == right.count && Set(left.map { $0.0 }).count == left.count &&
                zip(left, right).allSatisfy { pair in
                    pair.0.0 == pair.1.0 && schema(pair.0.1, pair.1.1, depth: depth + 1)
                }
        }
        return schema(lhs, rhs, depth: 0)
    }
}

private extension XMLElement {
    var elements: [XMLElement] { (children ?? []).compactMap { $0 as? XMLElement } }
}
