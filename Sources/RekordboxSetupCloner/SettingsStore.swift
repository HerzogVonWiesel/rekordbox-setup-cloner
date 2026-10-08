import AppKit
import CryptoKit
import Foundation

final class SettingsStore {
    private let fm = FileManager.default
    static var defaultDirectory: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Pioneer/rekordbox6", isDirectory: true)
    }
    static var recoveryDirectory: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Rekordbox Setup Cloner/Restore Points", isDirectory: true)
    }

    private func requireRekordboxClosed() throws {
        let running = NSWorkspace.shared.runningApplications.contains { application in
            let name = application.localizedName?.lowercased() ?? ""
            let identifier = application.bundleIdentifier?.lowercased() ?? ""
            let mainName = name.range(of: "^rekordbox(?:\\s*[0-9]+)?$", options: .regularExpression) != nil
            let mainIdentifier = ["com.pioneerdj.rekordboxdj", "com.pioneerdj.rekordbox",
                                  "com.pioneer.rekordbox", "com.alphatheta.rekordbox",
                                  "com.alphatheta.rekordboxdj"].contains(identifier)
            return mainName || mainIdentifier
        }
        guard !running else {
            throw SetupError("Quit rekordbox before saving, importing, copying Pad FX, or restoring settings. This also lets rekordbox finish saving your preferences.")
        }
    }

    private func root(_ directory: URL) throws -> URL {
        let url = directory.standardizedFileURL
        guard url.isFileURL, url.path == url.resolvingSymlinksInPath().path else {
            throw SetupError("Choose the actual settings folder, rather than a symbolic link.")
        }
        let values = try url.resourceValues(forKeys: [.isDirectoryKey])
        guard values.isDirectory == true else { throw SetupError("The settings location is not a folder.") }
        return url
    }

    private func fileURL(_ relative: String, in directory: URL) throws -> URL {
        guard SettingsPolicy.validRelativePath(relative) else { throw SetupError("Invalid relative settings path.") }
        var url = directory
        for component in relative.components(separatedBy: "/") {
            url.appendPathComponent(component)
            // lstat-backed attributes also catch dangling symbolic links.
            if let attributes = try? fm.attributesOfItem(atPath: url.path),
               attributes[.type] as? FileAttributeType == .typeSymbolicLink {
                throw SetupError("Symbolic links are not supported: \(relative)")
            }
        }
        guard url.standardizedFileURL.path.hasPrefix(directory.path + "/"),
              url.standardizedFileURL.path == url.resolvingSymlinksInPath().path else {
            throw SetupError("Settings path leaves the selected folder: \(relative)")
        }
        return url
    }

    private func read(_ relative: String, in directory: URL) throws -> Data? {
        let url = try fileURL(relative, in: directory)
        guard fm.fileExists(atPath: url.path) else { return nil }
        return try boundedData(url, maximum: SettingsPolicy.maxFileSize)
    }

    private func boundedData(_ url: URL, maximum: Int) throws -> Data {
        let attributes = try url.resourceValues(forKeys: [.isRegularFileKey, .fileSizeKey])
        guard attributes.isRegularFile == true, let size = attributes.fileSize, size <= maximum else {
            throw SetupError("Not a supported file, or too large: \(url.lastPathComponent)")
        }
        let data = try Data(contentsOf: url)
        guard data.count <= maximum else { throw SetupError("File exceeds the size limit.") }
        return data
    }

    private func version(in main: Data) throws -> RekordboxVersion {
        let properties = try SettingsXML.scalars(main)
        let raw = (properties["LaunchedVersion"] ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        // Informational metadata only. Compatibility is checked against destination settings.
        return RekordboxVersion(raw: raw.count <= 64 && raw.rangeOfCharacter(from: .controlCharacters) == nil ? raw : "")
    }

    private func auxiliaryPaths(in directory: URL) throws -> [String] {
        var paths: [String] = []
        for url in try fm.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles]) {
            let name = url.lastPathComponent
            if SettingsPolicy.fileGroup(name) != nil { paths.append(name) }
            guard ["KeyMappings", "MidiMappings", "pad"].contains(name) else { continue }
            _ = try fileURL(name, in: directory)
            var enumerationError: Error?
            guard let enumerator = fm.enumerator(at: url, includingPropertiesForKeys: [.isSymbolicLinkKey], options: [.skipsHiddenFiles], errorHandler: { _, error in
                enumerationError = error
                return false
            }) else { throw SetupError("Could not read \(name).") }
            while let child = enumerator.nextObject() as? URL {
                let relative = String(child.path.dropFirst(directory.path.count + 1))
                if SettingsPolicy.fileGroup(relative) != nil { paths.append(relative) }
                guard paths.count <= SettingsPolicy.maxFiles else { throw SetupError("Too many mapping files.") }
            }
            if let error = enumerationError { throw error }
        }
        return paths.sorted()
    }

    func inspect(_ directory: URL) throws -> SettingsInspection {
        let directory = try root(directory)
        guard let main = try read(SettingsPolicy.mainFile, in: directory) else {
            throw SetupError("This folder does not contain rekordbox3.settings.")
        }
        let values = try SettingsXML.scalars(main)
        var counts: [SettingsGroup: Int] = [:]
        for file in SettingsPolicy.preferenceFiles {
            guard let data = try read(file, in: directory) else { continue }
            for key in try SettingsXML.scalars(data, file: file).keys {
                if let group = SettingsPolicy.group(for: key, in: file) { counts[group, default: 0] += 1 }
            }
            for key in try SettingsXML.structures(data, file: file).keys {
                if let group = SettingsPolicy.structureGroup(for: key, in: file) { counts[group, default: 0] += 1 }
            }
        }
        for path in try auxiliaryPaths(in: directory) {
            if let group = SettingsPolicy.fileGroup(path) { counts[group, default: 0] += 1 }
        }
        return SettingsInspection(version: try version(in: main), counts: counts,
                                  stemsEnabled: values["TrackSeparationEnable"].map { $0 == "1" })
    }

    func capture(_ directory: URL, name: String, groups: Set<SettingsGroup>) throws -> SetupProfile {
        try requireRekordboxClosed()
        guard !groups.isEmpty else { throw SetupError("Select at least one settings group.") }
        let directory = try root(directory)
        guard let main = try read(SettingsPolicy.mainFile, in: directory) else { throw SetupError("Missing rekordbox3.settings.") }
        var properties: [String: [String: String]] = [:]
        var structures: [String: [String: Data]] = [:]
        for file in SettingsPolicy.preferenceFiles where !SettingsPolicy.groups(in: file).isDisjoint(with: groups) {
            guard let data = try read(file, in: directory) else { continue }
            let values = try SettingsXML.scalars(data, file: file).filter { key, _ in
                SettingsPolicy.group(for: key, in: file).map { groups.contains($0) } ?? false
            }
            if !values.isEmpty { properties[file] = values }
            let fragments = try SettingsXML.structures(data, file: file).filter { key, _ in
                SettingsPolicy.structureGroup(for: key, in: file).map { groups.contains($0) } ?? false
            }
            if !fragments.isEmpty { structures[file] = fragments }
        }
        var files: [String: Data] = [:]
        for path in try auxiliaryPaths(in: directory) {
            guard let group = SettingsPolicy.fileGroup(path), groups.contains(group) else { continue }
            guard let data = try read(path, in: directory) else { throw SetupError("Settings changed while being read. Try again.") }
            files[path] = data
        }
        let profile = SetupProfile(format: "rekordbox-setup", schemaVersion: 2, createdAt: Date(),
                                   name: name.trimmingCharacters(in: .whitespacesAndNewlines),
                                   rekordboxVersion: try version(in: main),
                                   groups: SettingsGroup.allCases.filter { groups.contains($0) },
                                   properties: properties, files: files, structures: structures)
        try validate(profile)
        try requireRekordboxClosed()
        return profile
    }

    private func validateAuxiliary(_ data: Data, path: String) throws {
        guard data.count <= SettingsPolicy.maxFileSize else { throw SetupError("Settings file too large: \(path)") }
        if path.hasSuffix(".xml") || path.hasSuffix(".mappings") {
            _ = try SettingsXML.parse(data)
        } else {
            guard let text = String(data: data, encoding: .utf8), !text.contains("\0") else {
                throw SetupError("Unsupported mapping encoding: \(path)")
            }
        }
    }

    private func validate(_ profile: SetupProfile) throws {
        guard profile.format == "rekordbox-setup", [1, 2].contains(profile.schemaVersion),
              profile.schemaVersion != 1 || profile.structuredPreferences.isEmpty,
              !profile.name.isEmpty, profile.name.count <= 120,
              profile.rekordboxVersion.raw.count <= 64,
              profile.rekordboxVersion.raw.rangeOfCharacter(from: .controlCharacters) == nil,
              !profile.groups.isEmpty, Set(profile.groups).count == profile.groups.count,
              profile.files.count <= SettingsPolicy.maxFiles,
              profile.preferenceCount + profile.files.count > 0 else {
            throw SetupError("This is not a supported setup backup, or it contains no settings.")
        }
        for (file, properties) in profile.properties {
            guard SettingsPolicy.preferenceFiles.contains(file),
                  profile.schemaVersion != 1 || [SettingsPolicy.mainFile, SettingsPolicy.playFile].contains(file) else {
                throw SetupError("Unapproved settings file: \(file)")
            }
            for (key, value) in properties {
                guard let group = SettingsPolicy.group(for: key, in: file, schemaVersion: profile.schemaVersion), profile.groups.contains(group),
                      value.utf8.count <= 4096, !value.contains("\0") else {
                    throw SetupError("Unapproved or invalid preference: \(key)")
                }
            }
        }
        for (file, fragments) in profile.structuredPreferences {
            guard SettingsPolicy.preferenceFiles.contains(file) else { throw SetupError("Unapproved preference file: \(file)") }
            for (key, data) in fragments {
                guard let group = SettingsPolicy.structureGroup(for: key, in: file), profile.groups.contains(group),
                      profile.properties[file]?[key] == nil else { throw SetupError("Unapproved or duplicate preference: \(key)") }
                _ = try SettingsXML.validatedStructure(data, key: key, file: file)
            }
        }
        for (path, data) in profile.files {
            guard let group = SettingsPolicy.fileGroup(path), profile.groups.contains(group) else {
                throw SetupError("Unapproved file in backup: \(path)")
            }
            try validateAuxiliary(data, path: path)
        }
    }

    private func encoder() -> JSONEncoder {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }

    private func decoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }

    func loadProfile(_ url: URL) throws -> SetupProfile {
        let profile = try decoder().decode(SetupProfile.self, from: boundedData(url, maximum: SettingsPolicy.maxArchiveSize))
        try validate(profile)
        return profile
    }

    func saveProfile(_ profile: SetupProfile, to url: URL, settingsDirectory: URL) throws {
        try validate(profile)
        let resolved = url.standardizedFileURL.resolvingSymlinksInPath()
        let pioneer = SettingsStore.defaultDirectory.deletingLastPathComponent().resolvingSymlinksInPath()
        let selected = settingsDirectory.standardizedFileURL.resolvingSymlinksInPath()
        guard ![pioneer, selected].contains(where: { resolved.path == $0.path || resolved.path.hasPrefix($0.path + "/") }) else {
            throw SetupError("Save the portable backup outside rekordbox's settings folder.")
        }
        let data = try encoder().encode(profile)
        guard data.count <= SettingsPolicy.maxArchiveSize else { throw SetupError("Backup exceeds the size limit.") }
        try data.write(to: url, options: .atomic)
    }

    func plan(_ profile: SetupProfile, destination: URL, groups: Set<SettingsGroup>) throws -> ImportPlan {
        try requireRekordboxClosed()
        try validate(profile)
        let directory = try root(destination)
        guard let main = try read(SettingsPolicy.mainFile, in: directory) else { throw SetupError("Missing rekordbox3.settings.") }
        let targetVersion = try version(in: main)
        var edits: [FileEdit] = []
        var changes: [Change] = []
        var skipped: [String] = []
        var observed = [SettingsPolicy.mainFile: main]
        for file in Set(profile.properties.keys).union(profile.structuredPreferences.keys).sorted() {
            let incoming = (profile.properties[file] ?? [:]).filter { key, _ in
                SettingsPolicy.group(for: key, in: file).map { groups.contains($0) } ?? false
            }
            let incomingStructures = (profile.structuredPreferences[file] ?? [:]).filter { key, _ in
                SettingsPolicy.structureGroup(for: key, in: file).map { groups.contains($0) } ?? false
            }
            guard !incoming.isEmpty || !incomingStructures.isEmpty else { continue }
            guard let before = try read(file, in: directory) else {
                skipped.append("\(file): not present on this Mac")
                continue
            }
            observed[file] = before
            let current: [String: String]
            let currentElements: [String: XMLElement]
            do {
                current = try SettingsXML.scalars(before, file: file)
                currentElements = try SettingsXML.elements(SettingsXML.parse(before))
            } catch {
                skipped.append("\(file): destination preference structure is unsupported")
                continue
            }
            var updates: [String: String] = [:]
            for key in incoming.keys.sorted() {
                guard let oldValue = current[key] else {
                    skipped.append("\(key): not present on this Mac")
                    continue
                }
                let newValue = incoming[key]!
                guard oldValue != newValue else { continue }
                updates[key] = newValue
                changes.append(Change(id: "\(file)/\(key)", group: SettingsPolicy.group(for: key, in: file)!,
                                      title: key, detail: "\(oldValue) → \(newValue)"))
            }
            var structureUpdates: [String: Data] = [:]
            for key in incomingStructures.keys.sorted() {
                guard let original = currentElements[key] else {
                    skipped.append("\(key): not present on this Mac")
                    continue
                }
                let fragment = incomingStructures[key]!
                let replacement = try SettingsXML.validatedStructure(fragment, key: key, file: file)
                guard try SettingsXML.compatibleStructure(original, replacement, key: key, file: file) else {
                    skipped.append("\(key): display/effect fields differ on this Mac")
                    continue
                }
                guard !SettingsXML.structureEqual(original, replacement) else { continue }
                structureUpdates[key] = fragment
                changes.append(Change(id: "\(file)/\(key)", group: SettingsPolicy.structureGroup(for: key, in: file)!,
                                      title: key, detail: file == SettingsPolicy.browserFile ?
                                        "Update browser display (order, visibility, sizing or sorting)" : "Update Groove Circuit effect beats"))
            }
            if !updates.isEmpty || !structureUpdates.isEmpty {
                edits.append(FileEdit(path: file, before: before,
                                      after: try SettingsXML.merging(updates, structures: structureUpdates, into: before, file: file)))
            }
        }
        var padCompatible = false
        if groups.contains(.mappings), profile.files.keys.contains(where: { $0.hasPrefix("pad/") }) {
            if let current = try read("pad/Version", in: directory) {
                observed["pad/Version"] = current
                padCompatible = profile.files["pad/Version"] == current
            }
        }
        for path in profile.files.keys.sorted() {
            guard let group = SettingsPolicy.fileGroup(path), groups.contains(group) else { continue }
            if path.hasPrefix("pad/") && !padCompatible {
                skipped.append("\(path): pad format version differs or is unknown")
                continue
            }
            let before = try read(path, in: directory)
            if let before { observed[path] = before }
            // Different rekordbox releases use different FX filenames. Never guess a conversion.
            if group == .effects && before == nil {
                skipped.append("\(path): not present on this Mac")
                continue
            }
            let after = profile.files[path]!
            guard before != after else { continue }
            if let before, (path.hasSuffix(".xml") || path.hasSuffix(".mappings")),
               !SettingsXML.compatibleFile(before, after) {
                skipped.append("\(path): XML file format differs or is unsupported on this Mac")
                continue
            }
            edits.append(FileEdit(path: path, before: before, after: after))
            changes.append(Change(id: path, group: group, title: path,
                                  detail: before == nil ? "Add mapping file" : "Replace settings file"))
        }
        return ImportPlan(destination: directory, profile: profile, targetVersion: targetVersion,
                          edits: edits, changes: changes, skipped: skipped, observed: observed)
    }

    private func digest(_ data: Data?) -> String? {
        data.map { SHA256.hash(data: $0).map { String(format: "%02x", $0) }.joined() }
    }

    func planPadFXCopy(in destination: URL, direction: PadFXCopyDirection, banks: PadFXBanks) throws -> PadFXCopyPlan {
        try requireRekordboxClosed()
        let directory = try root(destination)
        guard let main = try read(SettingsPolicy.mainFile, in: directory) else { throw SetupError("Missing rekordbox3.settings.") }
        let targetVersion = try version(in: main)
        guard let before = try read(PadFXSettings.filename, in: directory) else { throw SetupError("Missing PadFxSettings.xml.") }
        let copy = try PadFXSettings.copying(before, direction: direction, banks: banks)
        let edits = copy.changes.isEmpty ? [] : [FileEdit(path: PadFXSettings.filename, before: before, after: copy.data)]
        return PadFXCopyPlan(destination: directory, direction: direction, banks: banks,
                             version: targetVersion, mainSettings: main, edits: edits, changes: copy.changes)
    }

    func apply(_ plan: PadFXCopyPlan) throws -> URL {
        guard try read(SettingsPolicy.mainFile, in: plan.destination) == plan.mainSettings else {
            throw SetupError("Settings changed since the preview. Create a fresh preview before copying Pad FX.")
        }
        return try commit(plan.edits, destination: plan.destination,
                          name: "Pad FX copy: \(plan.direction.title), \(plan.banks.title)")
    }

    private func writeRecovery(_ entries: [FileEdit], destination: URL, name: String) throws -> URL {
        let directory = SettingsStore.recoveryDirectory
        try fm.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        guard directory.standardizedFileURL.path == directory.resolvingSymlinksInPath().path else {
            throw SetupError("The restore-point folder must not be a symbolic link.")
        }
        try fm.setAttributes([.posixPermissions: 0o700], ofItemAtPath: directory.path)
        let archive = RecoveryArchive(format: "rekordbox-local-recovery", schemaVersion: 1,
                                      createdAt: Date(), destination: destination.path, profileName: name,
                                      entries: entries.map { RecoveryEntry(path: $0.path, original: $0.before, appliedSHA256: digest($0.after)) })
        let data = try encoder().encode(archive)
        guard data.count <= SettingsPolicy.maxArchiveSize else { throw SetupError("Restore point exceeds the size limit.") }
        let date = ISO8601DateFormatter().string(from: archive.createdAt).replacingOccurrences(of: ":", with: "-")
        let url = directory.appendingPathComponent("\(date)-\(UUID().uuidString.prefix(8)).rbrecovery")
        try data.write(to: url, options: .withoutOverwriting)
        try fm.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url.path)
        return url
    }

    private func write(_ data: Data?, path: String, in directory: URL) throws {
        let url = try fileURL(path, in: directory)
        if let data {
            let attributes = try? fm.attributesOfItem(atPath: url.path)
            try fm.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            _ = try fileURL(path, in: directory)
            try data.write(to: url, options: .atomic)
            let permissions = attributes?[.posixPermissions] ?? NSNumber(value: 0o600)
            try fm.setAttributes([.posixPermissions: permissions], ofItemAtPath: url.path)
        } else if fm.fileExists(atPath: url.path) {
            try fm.removeItem(at: url)
        }
    }

    /// A complete local restore point is persisted before the first destination write.
    private func commit(_ edits: [FileEdit], destination: URL, name: String) throws -> URL {
        try requireRekordboxClosed()
        _ = try root(destination)
        guard !edits.isEmpty else { throw SetupError("There are no changes to apply.") }
        for edit in edits {
            guard try read(edit.path, in: destination) == edit.before else {
                throw SetupError("Settings changed since the preview. Create a fresh preview before applying changes.")
            }
        }
        let recovery = try writeRecovery(edits, destination: destination, name: name)
        var attempted: [FileEdit] = []
        do {
            try requireRekordboxClosed()
            for edit in edits {
                attempted.append(edit)
                try write(edit.after, path: edit.path, in: destination)
            }
        } catch {
            var rollbackFailures: [String] = []
            for edit in attempted.reversed() {
                do { try write(edit.before, path: edit.path, in: destination) }
                catch { rollbackFailures.append(edit.path) }
            }
            let result = rollbackFailures.isEmpty ? "The original files were restored." :
                "Some files could not be restored: \(rollbackFailures.joined(separator: ", ")). Use the saved restore point."
            throw SetupError("Settings update failed: \(error.localizedDescription)\n\(result)\nRestore point: \(recovery.path)")
        }
        return recovery
    }

    func apply(_ plan: ImportPlan) throws -> URL {
        for (path, data) in plan.observed {
            guard try read(path, in: plan.destination) == data else {
                throw SetupError("Settings changed since the preview. Create a fresh preview before importing.")
            }
        }
        return try commit(plan.edits, destination: plan.destination, name: plan.profile.name)
    }

    func loadRecovery(_ url: URL, destination: URL) throws -> RecoveryArchive {
        let archive = try decoder().decode(RecoveryArchive.self, from: boundedData(url, maximum: SettingsPolicy.maxArchiveSize))
        let directory = try root(destination)
        guard archive.format == "rekordbox-local-recovery", archive.schemaVersion == 1,
              archive.destination == directory.path, !archive.entries.isEmpty,
              archive.entries.count <= SettingsPolicy.maxFiles + SettingsPolicy.preferenceFiles.count,
              Set(archive.entries.map(\.path)).count == archive.entries.count else {
            throw SetupError("This restore point is invalid or belongs to a different settings folder. Select the folder on the Mac where it was created.")
        }
        for entry in archive.entries {
            guard SettingsPolicy.preferenceFiles.contains(entry.path) || SettingsPolicy.fileGroup(entry.path) != nil else {
                throw SetupError("Unapproved restore path: \(entry.path)")
            }
            if entry.path == SettingsPolicy.mainFile && entry.original == nil { throw SetupError("Invalid main settings restore entry.") }
            if let data = entry.original {
                if SettingsPolicy.preferenceFiles.contains(entry.path) { _ = try SettingsXML.parse(data) }
                else { try validateAuxiliary(data, path: entry.path) }
            }
        }
        return archive
    }

    func recoveryEdits(_ archive: RecoveryArchive, destination: URL) throws -> (edits: [FileEdit], changed: [String]) {
        try requireRekordboxClosed()
        let directory = try root(destination)
        guard archive.destination == directory.path else { throw SetupError("Restore point belongs to a different folder.") }
        var edits: [FileEdit] = []
        var changed: [String] = []
        for entry in archive.entries {
            let current = try read(entry.path, in: directory)
            guard current != entry.original else { continue }
            if digest(current) != entry.appliedSHA256 { changed.append(entry.path) }
            edits.append(FileEdit(path: entry.path, before: current, after: entry.original))
        }
        return (edits, changed)
    }

    func restore(_ edits: [FileEdit], destination: URL) throws -> URL {
        try commit(edits, destination: destination, name: "Before restoring previous setup")
    }
}
