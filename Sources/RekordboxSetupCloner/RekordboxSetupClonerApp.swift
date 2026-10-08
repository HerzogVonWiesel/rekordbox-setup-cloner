import SwiftUI

@main
struct RekordboxSetupClonerApp: App {
    @StateObject private var model = AppModel()

    var body: some Scene {
        WindowGroup("Rekordbox Setup Cloner") {
            ContentView(model: model)
                .frame(minWidth: 860, minHeight: 710)
        }
        .defaultSize(width: 960, height: 780)
        .windowResizability(.contentMinSize)
        .commands {
            CommandGroup(replacing: .newItem) {}
        }
    }
}

struct ContentView: View {
    @ObservedObject var model: AppModel

    private var mainContent: some View {
        VStack(alignment: .leading, spacing: 22) {
            HStack(alignment: .top) {
                Image(nsImage: NSImage(named: "AppIcon") ?? NSImage())
                    .resizable()
                    .frame(width: 64, height: 64)
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: 7) {
                    Text("Your decks. Your setup.")
                        .font(.largeTitle.bold())
                    Text("Bring your rekordbox preferences to another Mac.")
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Label("Preferences only", systemImage: "slider.horizontal.3")
                    .font(.callout.weight(.medium))
                    .padding(10)
                    .background(Color.accentColor.opacity(0.1), in: RoundedRectangle(cornerRadius: 9))
            }

            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    Label("Settings on this Mac", systemImage: "folder")
                        .font(.headline)
                    Spacer()
                    if let inspection = model.inspection {
                        Text(inspection.version.label).font(.callout.monospacedDigit())
                        if let enabled = inspection.stemsEnabled {
                            Text(enabled ? "STEMS on" : "STEMS off")
                                .font(.caption.weight(.semibold))
                                .padding(.horizontal, 8).padding(.vertical, 4)
                                .background((enabled ? Color.green : Color.secondary).opacity(0.12), in: Capsule())
                        }
                    }
                }
                HStack(spacing: 12) {
                    Text(model.directory.path)
                        .font(.system(.callout, design: .monospaced))
                        .textSelection(.enabled)
                        .lineLimit(2)
                    Spacer(minLength: 0)
                    Button("Choose…", action: model.chooseDirectory)
                    Button("Inspect", action: model.inspect)
                }
                Text("rekordbox 7 also stores settings in the folder named rekordbox6.")
                    .font(.caption).foregroundStyle(.secondary)
            }
            .padding(16)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 12))

            HStack(alignment: .top, spacing: 24) {
                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        Text("WHAT TO TRANSFER").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                        Spacer()
                        Button("All") { model.selectedGroups = Set(SettingsGroup.allCases) }
                            .buttonStyle(.link)
                        Button("None") { model.selectedGroups.removeAll() }
                            .buttonStyle(.link)
                    }
                    ScrollView {
                        LazyVStack(alignment: .leading, spacing: 12) {
                            ForEach(SettingsGroup.allCases) { group in
                                groupToggle(group)
                            }
                        }
                        .padding(.trailing, 8)
                    }
                    .frame(height: 350)
                    Text("\(model.selectedGroups.count) of \(SettingsGroup.allCases.count) groups selected. Scroll for all groups.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Divider()

                VStack(alignment: .leading, spacing: 18) {
                    VStack(alignment: .leading, spacing: 12) {
                        Label("Save your setup", systemImage: "square.and.arrow.up")
                            .font(.title3.weight(.semibold))
                        Text("Create a portable backup of the selected preferences.")
                            .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                        TextField("Setup name", text: $model.profileName)
                            .textFieldStyle(.roundedBorder)
                            .accessibilityLabel("Setup name")
                        Button(action: model.save) {
                            Text("Save setup…").frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.borderedProminent)
                        .controlSize(.large)
                    }
                    Divider()
                    VStack(alignment: .leading, spacing: 12) {
                        Label("Use a saved setup", systemImage: "square.and.arrow.down")
                            .font(.title3.weight(.semibold))
                        Text("Preview changes for the selected groups. Importing saves this Mac’s current settings first.")
                            .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                        Button(action: model.previewImport) {
                            Text("Open backup & preview…").frame(maxWidth: .infinity)
                        }
                        .controlSize(.large)
                    }
                }
                .frame(width: 295)
                .disabled(model.selectedGroups.isEmpty)
            }
            .fixedSize(horizontal: false, vertical: true)

            VStack(alignment: .leading, spacing: 12) {
                Label("Copy Pad FX between decks", systemImage: "square.on.square")
                    .font(.title3.weight(.semibold))
                Text("Copy a deck’s Pad FX assignments and parameters in the selected settings folder.")
                    .foregroundStyle(.secondary)
                HStack(alignment: .bottom, spacing: 20) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Direction").font(.caption).foregroundStyle(.secondary)
                        Picker("Copy direction", selection: $model.padFXDirection) {
                            ForEach(PadFXCopyDirection.allCases) { direction in
                                Text(direction.title).tag(direction)
                            }
                        }
                        .labelsHidden()
                        .pickerStyle(.segmented)
                    }
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Banks").font(.caption).foregroundStyle(.secondary)
                        Picker("Pad FX banks", selection: $model.padFXBanks) {
                            ForEach(PadFXBanks.allCases) { banks in
                                Text(banks.title).tag(banks)
                            }
                        }
                        .labelsHidden()
                        .pickerStyle(.segmented)
                    }
                    Button("Preview copy…", action: model.previewPadFXCopy)
                }
            }
            .padding(16)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 12))

            Spacer(minLength: 0)
            Divider()
            VStack(alignment: .leading, spacing: 10) {
                Text("Music, playlists, login details and audio-device routing stay on their own Mac.")
                    .font(.callout).foregroundStyle(.secondary)
                HStack(alignment: .center, spacing: 16) {
                    Text(model.status).font(.callout).fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, alignment: .leading)
                    if model.lastOutput != nil {
                        Button("Show in Finder", action: model.revealOutput)
                    }
                    Button("Restore previous setup…", action: model.chooseRecovery)
                }
            }
        }
        .padding(28)
    }

    private func groupToggle(_ group: SettingsGroup) -> some View {
        Toggle(isOn: Binding(get: { model.selectedGroups.contains(group) }, set: { enabled in
            if enabled { model.selectedGroups.insert(group) }
            else { model.selectedGroups.remove(group) }
        })) {
            HStack(alignment: .top, spacing: 10) {
                Image(systemName: group.symbol)
                    .foregroundStyle(Color.accentColor)
                    .frame(width: 22)
                    .padding(.top, 2)
                VStack(alignment: .leading, spacing: 3) {
                    HStack {
                        Text(group.title).fontWeight(.medium)
                        Spacer()
                        if let count = model.inspection?.counts[group] {
                            Text("\(count)").font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                        }
                    }
                    Text(group.detail).font(.caption).foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .padding(.vertical, 4)
        }
        .toggleStyle(.checkbox)
    }

    var body: some View {
        ScrollView { mainContent }
        .sheet(isPresented: $model.showPreview) {
            if let plan = model.plan { ImportPreview(model: model, plan: plan) }
        }
        .sheet(item: $model.padFXPlan) { plan in
            PadFXCopyPreview(model: model, plan: plan)
        }
        .alert("Couldn’t complete the operation", isPresented: Binding(
            get: { model.errorMessage != nil },
            set: { if !$0 { model.errorMessage = nil } }
        )) {
            Button("OK") { model.errorMessage = nil }
        } message: {
            Text(model.errorMessage ?? "")
        }
    }
}

struct ImportPreview: View {
    @ObservedObject var model: AppModel
    let plan: ImportPlan

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Preview “\(plan.profile.name)”").font(.title2.bold())
            Text("Saved \(plan.profile.createdAt.formatted(date: .abbreviated, time: .shortened)) · \(plan.profile.rekordboxVersion.label)")
                .foregroundStyle(.secondary)
            Text("Destination: \(plan.destination.path)")
                .font(.caption.monospaced()).textSelection(.enabled)

            if let notice = plan.compatibilityNotice {
                Label(notice, systemImage: "info.circle")
                    .font(.callout).foregroundStyle(.orange)
            }

            Text("\(plan.changes.count) changes across \(plan.edits.count) files")
                .font(.headline)
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    if plan.changes.isEmpty {
                        Text("No changes to apply. The selected settings already match, or none are compatible with this Mac.")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(SettingsGroup.allCases) { group in
                        let changes = plan.changes.filter { $0.group == group }
                        if !changes.isEmpty {
                            VStack(alignment: .leading, spacing: 8) {
                                Label(group.title, systemImage: group.symbol).font(.headline)
                                ForEach(changes) { change in
                                    HStack(alignment: .top, spacing: 16) {
                                        Text(change.title).font(.system(.caption, design: .monospaced))
                                            .textSelection(.enabled)
                                        Spacer(minLength: 12)
                                        Text(change.detail).font(.caption).foregroundStyle(.secondary)
                                            .multilineTextAlignment(.trailing)
                                    }
                                }
                            }
                            .padding(12)
                            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 8))
                        }
                    }
                    if !plan.skipped.isEmpty {
                        DisclosureGroup("Skipped (\(plan.skipped.count))") {
                            VStack(alignment: .leading, spacing: 5) {
                                ForEach(Array(plan.skipped.enumerated()), id: \.offset) { _, reason in
                                    Text(reason).font(.caption).foregroundStyle(.secondary)
                                }
                            }.frame(maxWidth: .infinity, alignment: .leading).padding(.top, 8)
                        }
                    }
                }.frame(maxWidth: .infinity, alignment: .leading)
            }
            Divider()
            Text("Import saves a private, local restore point before changing files. Keep rekordbox closed until the import finishes.")
                .font(.callout).foregroundStyle(.secondary)
            HStack {
                Button("Cancel") { model.showPreview = false }
                    .keyboardShortcut(.cancelAction)
                Spacer()
                Button("Save restore point & import", action: model.applyImport)
                    .buttonStyle(.borderedProminent)
                    .disabled(plan.edits.isEmpty)
            }
        }
        .padding(24)
        .frame(width: 730, height: 620)
    }
}

struct PadFXCopyPreview: View {
    @ObservedObject var model: AppModel
    let plan: PadFXCopyPlan

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Copy Pad FX: \(plan.direction.title)").font(.title2.bold())
            Text("\(plan.banks.title) · \(plan.version.label)").foregroundStyle(.secondary)
            Text("Destination: \(plan.destination.path)")
                .font(.caption.monospaced()).textSelection(.enabled)
            Text("\(plan.changes.count) pad slots will change on deck \(plan.direction.targetDeck)")
                .font(.headline)
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    if plan.changes.isEmpty {
                        Text("The selected Pad FX banks already match. No copy is needed.")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(plan.changes) { change in
                        VStack(alignment: .leading, spacing: 5) {
                            Text(change.title).fontWeight(.medium)
                            Text(change.detail).font(.caption.monospaced())
                                .foregroundStyle(.secondary).textSelection(.enabled)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 8))
                    }
                }.frame(maxWidth: .infinity, alignment: .leading)
            }
            Divider()
            Text("Copy replaces the selected banks on deck \(plan.direction.targetDeck) and saves a local restore point first. Keep rekordbox closed until it finishes.")
                .font(.callout).foregroundStyle(.secondary)
            HStack {
                Button("Cancel") { model.padFXPlan = nil }
                    .keyboardShortcut(.cancelAction)
                Spacer()
                Button("Save restore point & copy", action: model.applyPadFXCopy)
                    .buttonStyle(.borderedProminent)
                    .disabled(plan.edits.isEmpty)
            }
        }
        .padding(24)
        .frame(width: 730, height: 620)
    }
}
