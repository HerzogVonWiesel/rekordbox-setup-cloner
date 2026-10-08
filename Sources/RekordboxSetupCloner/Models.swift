import Foundation

struct SetupError: LocalizedError {
    let message: String
    init(_ message: String) { self.message = message }
    var errorDescription: String? { message }
}

struct RekordboxVersion: Codable, Equatable {
    let raw: String
    var components: [Int]? {
        if raw.count == 5, let number = Int(raw), number >= 60000, number < 100000 {
            return [number / 10000, (number / 100) % 100, number % 100]
        }
        let pieces = raw.split(separator: ".")
        guard pieces.count >= 3, let major = Int(pieces[0]), let minor = Int(pieces[1]), let patch = Int(pieces[2]) else { return nil }
        return [major, minor, patch]
    }
    var major: Int? { components?.first }
    var display: String { components?.map(String.init).joined(separator: ".") ?? "Unknown" }
    var label: String { components != nil ? "rekordbox \(display)" : "rekordbox version unavailable" }

    func compatibilityNotice(destination: RekordboxVersion) -> String? {
        guard let source = components, let target = destination.components else {
            return "The rekordbox version is unavailable for the backup or destination. Matching preferences can still be imported; missing settings and incompatible file formats are skipped."
        }
        guard source != target else { return nil }
        return "The backup uses rekordbox \(display); the destination uses \(destination.display). Versions differ, so only matching preferences and compatible file formats will be transferred."
    }
}

struct SetupProfile: Codable {
    let format: String
    let schemaVersion: Int
    let createdAt: Date
    let name: String
    let rekordboxVersion: RekordboxVersion
    let groups: [SettingsGroup]
    /// Only approved scalar properties, never the full main settings file.
    let properties: [String: [String: String]]
    let files: [String: Data]
    /// Approved display/FX XML fragments only. Absent in version 1 backups.
    let structures: [String: [String: Data]]?

    var structuredPreferences: [String: [String: Data]] { structures ?? [:] }
    var preferenceCount: Int {
        properties.values.reduce(0) { $0 + $1.count } + structuredPreferences.values.reduce(0) { $0 + $1.count }
    }
}

struct Change: Identifiable {
    let id: String
    let group: SettingsGroup
    let title: String
    let detail: String
}

struct FileEdit {
    let path: String
    let before: Data?
    let after: Data?
}

struct ImportPlan {
    let destination: URL
    let profile: SetupProfile
    let targetVersion: RekordboxVersion
    let edits: [FileEdit]
    let changes: [Change]
    let skipped: [String]
    /// Includes unchanged inspected files so a stale preview cannot be committed.
    let observed: [String: Data]
    var compatibilityNotice: String? { profile.rekordboxVersion.compatibilityNotice(destination: targetVersion) }
}

struct PadFXCopyPlan: Identifiable {
    let id = UUID()
    let destination: URL
    let direction: PadFXCopyDirection
    let banks: PadFXBanks
    let version: RekordboxVersion
    let mainSettings: Data
    let edits: [FileEdit]
    let changes: [Change]
}

struct RecoveryEntry: Codable {
    let path: String
    let original: Data?
    let appliedSHA256: String?
}

struct RecoveryArchive: Codable {
    let format: String
    let schemaVersion: Int
    let createdAt: Date
    let destination: String
    let profileName: String
    let entries: [RecoveryEntry]
}

struct SettingsInspection {
    let version: RekordboxVersion
    let counts: [SettingsGroup: Int]
    let stemsEnabled: Bool?
}
