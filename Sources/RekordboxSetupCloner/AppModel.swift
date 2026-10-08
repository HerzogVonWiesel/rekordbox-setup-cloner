import AppKit
import SwiftUI
import UniformTypeIdentifiers

extension UTType {
    static let rekordboxSetup = UTType(exportedAs: "local.rekordbox-setup-cloner.setup", conformingTo: .json)
    static let rekordboxRecovery = UTType(exportedAs: "local.rekordbox-setup-cloner.recovery", conformingTo: .json)
}

@MainActor
final class AppModel: ObservableObject {
    @Published var directory = SettingsStore.defaultDirectory
    @Published var selectedGroups = SettingsGroup.defaultSelection
    @Published var profileName = "My party setup"
    @Published var inspection: SettingsInspection?
    @Published var plan: ImportPlan?
    @Published var showPreview = false
    @Published var padFXDirection: PadFXCopyDirection = .deck1To2
    @Published var padFXBanks: PadFXBanks = .both
    @Published var padFXPlan: PadFXCopyPlan?
    @Published var errorMessage: String?
    @Published var status = "Choose your settings folder, then inspect it to get started."
    @Published var lastOutput: URL?
    private let store = SettingsStore()

    func inspect() {
        inspection = nil
        do {
            inspection = try store.inspect(directory)
            status = "Settings found. Quit rekordbox before saving or importing a setup."
        } catch { errorMessage = error.localizedDescription }
    }

    func chooseDirectory() {
        let panel = NSOpenPanel()
        panel.title = "Choose the rekordbox settings folder"
        panel.message = "Usually ~/Library/Application Support/Pioneer/rekordbox6, including for rekordbox 7."
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.allowsMultipleSelection = false
        panel.directoryURL = directory.deletingLastPathComponent()
        guard panel.runModal() == .OK, let url = panel.url else { return }
        directory = url
        inspection = nil
        plan = nil
        padFXPlan = nil
        inspect()
    }

    func save() {
        do {
            let profile = try store.capture(directory, name: profileName, groups: selectedGroups)
            let panel = NSSavePanel()
            panel.title = "Save portable setup"
            panel.allowedContentTypes = [.rekordboxSetup]
            panel.canCreateDirectories = true
            panel.nameFieldStringValue = "\(safeFilename(profile.name)).rbsetup"
            panel.message = "Take this .rbsetup file to your friend's Mac."
            guard panel.runModal() == .OK, let url = panel.url else { return }
            try store.saveProfile(profile, to: url, settingsDirectory: directory)
            lastOutput = url
            status = "Saved \(profile.preferenceCount) preferences and \(profile.files.count) settings files to \(url.lastPathComponent)."
        } catch { errorMessage = error.localizedDescription }
    }

    private func safeFilename(_ name: String) -> String {
        let cleaned = name.components(separatedBy: CharacterSet(charactersIn: "/\\:\n\r")).joined(separator: "-")
        return cleaned.isEmpty ? "My setup" : cleaned
    }

    func previewImport() {
        let panel = NSOpenPanel()
        panel.title = "Open a saved setup"
        panel.allowedContentTypes = [.rekordboxSetup, .json]
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let profile = try store.loadProfile(url)
            plan = try store.plan(profile, destination: directory, groups: selectedGroups)
            showPreview = true
        } catch { errorMessage = error.localizedDescription }
    }

    func applyImport() {
        guard let plan else { return }
        do {
            let recovery = try store.apply(plan)
            showPreview = false
            self.plan = nil
            lastOutput = recovery
            inspection = try? store.inspect(directory)
            status = "Imported “\(plan.profile.name)”. Your previous setup is saved locally. You can now open rekordbox."
        } catch {
            showPreview = false
            self.plan = nil
            errorMessage = error.localizedDescription
        }
    }

    func chooseRecovery() {
        let panel = NSOpenPanel()
        panel.title = "Restore this Mac's previous setup"
        panel.message = "Choose a local restore point created before an import, Pad FX copy, or restore on this Mac."
        panel.allowedContentTypes = [.rekordboxRecovery, .json]
        panel.allowsMultipleSelection = false
        panel.directoryURL = SettingsStore.recoveryDirectory
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let archive = try store.loadRecovery(url, destination: directory)
            let preview = try store.recoveryEdits(archive, destination: directory)
            guard !preview.edits.isEmpty else {
                status = "This setup is already restored."
                return
            }
            let alert = NSAlert()
            alert.messageText = "Restore the setup from \(archive.createdAt.formatted(date: .abbreviated, time: .shortened))?"
            var detail = "Restore point: \(archive.profileName)\n\nThis restores \(preview.edits.count) complete settings files in:\n\(directory.path)\n\nThe current files will be saved in a new local restore point first."
            if !preview.changed.isEmpty {
                detail += "\n\n\(preview.changed.count) files have later changes. Restoring will also undo those changes, including changes to other preferences in these files. These files are marked below."
            }
            alert.informativeText = detail
            let scroll = NSScrollView(frame: NSRect(x: 0, y: 0, width: 500, height: 180))
            scroll.hasVerticalScroller = true
            scroll.borderType = .bezelBorder
            let text = NSTextView(frame: scroll.bounds)
            text.isEditable = false
            text.isVerticallyResizable = true
            text.autoresizingMask = [.width]
            text.textContainer?.widthTracksTextView = true
            text.font = .monospacedSystemFont(ofSize: 11, weight: .regular)
            text.string = preview.edits.map { edit in
                preview.changed.contains(edit.path) ? "\(edit.path) — has later changes" : edit.path
            }.joined(separator: "\n")
            scroll.documentView = text
            alert.accessoryView = scroll
            alert.alertStyle = .warning
            alert.addButton(withTitle: "Cancel")
            alert.addButton(withTitle: "Restore setup")
            guard alert.runModal() == .alertSecondButtonReturn else { return }
            let recovery = try store.restore(preview.edits, destination: directory)
            lastOutput = recovery
            inspection = try? store.inspect(directory)
            status = "Previous setup restored. You can now open rekordbox."
        } catch { errorMessage = error.localizedDescription }
    }

    func revealOutput() {
        if let lastOutput { NSWorkspace.shared.activateFileViewerSelecting([lastOutput]) }
    }

    func previewPadFXCopy() {
        do {
            padFXPlan = try store.planPadFXCopy(in: directory, direction: padFXDirection, banks: padFXBanks)
        } catch { errorMessage = error.localizedDescription }
    }

    func applyPadFXCopy() {
        guard let plan = padFXPlan else { return }
        do {
            let recovery = try store.apply(plan)
            padFXPlan = nil
            lastOutput = recovery
            status = "Copied Pad FX: \(plan.direction.title) (\(plan.banks.title)). Previous settings saved in a local restore point."
        } catch {
            padFXPlan = nil
            errorMessage = error.localizedDescription
        }
    }
}
