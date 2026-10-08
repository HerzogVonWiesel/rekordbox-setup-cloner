# Rekordbox Setup Cloner

[![macOS builds](https://github.com/HerzogVonWiesel/rekordbox-setup-cloner/actions/workflows/macos.yml/badge.svg)](https://github.com/HerzogVonWiesel/rekordbox-setup-cloner/actions/workflows/macos.yml)

<p align="center"><img src="Resources/AppIcon.png" width="160" alt="Rekordbox Setup Cloner icon"></p>

A small native macOS app to take your rekordbox performance preferences to another Mac, and restore the host's setup afterwards. Requires **macOS 13 or later**. No third-party dependencies or network services.

## Download

Download the versioned **macos-universal.zip** from [Releases](https://github.com/HerzogVonWiesel/rekordbox-setup-cloner/releases), extract it, and move **Rekordbox Setup Cloner.app** to Applications. The same app runs on Apple Silicon and Intel Macs. Releases also include `SHA256SUMS.txt`.

This is currently a **preview**: builds compile successfully, but runtime and real rekordbox import/restore verification are still pending. Automatic builds do not launch the app or run application tests.

## Current status

Initial implementation based on read-only inspection of rekordbox **7.2.19** settings. **Runtime testing and actual rekordbox import/restore verification are pending owner approval.** The source supports the observed XML format, but compatibility with rekordbox 6 and other 7.x releases has not been verified.

The universal Mac application has been compiled successfully. It has not been launched or tested, and the existing rekordbox settings have not been modified during development.

rekordbox 7 uses `~/Library/Application Support/Pioneer/rekordbox6` on the inspected Mac. The app reads the `LaunchedVersion` property to determine the version; it does not infer it from the folder name.

## Build

With Xcode or the Xcode command-line tools installed:

```sh
bash scripts/build-app.sh
```

Produces a universal **Apple Silicon + Intel** app and a ZIP in `dist/`. The build script compiles, packages and ad-hoc signs the app; it does not run it or access rekordbox settings. The app is not Developer ID signed or notarized; a copied/downloaded build may need **System Settings → Privacy & Security → Open Anyway** on the other Mac.

`swift build` also compiles the executable for development. Use the packaged `.app` for the file-type declarations and normal Mac launch behaviour.

### Automatic builds and releases

GitHub Actions builds the universal Mac app for pushes to `main`, pull requests, and manual workflow runs. Download the ZIP/checksums from the workflow's artifacts (retained for 30 days).

To publish a release:

1. Update `CFBundleShortVersionString` in `Resources/Info.plist` and increment `CFBundleVersion`.
2. Commit and push the changes to `main`.
3. Create and push a matching version tag, for example:

   ```sh
   git tag -a v0.2.0 -m "Release v0.2.0"
   git push origin v0.2.0
   ```

The tag workflow builds the app, checks both binary architectures and its ad-hoc signature, then publishes the ZIP and checksum as a GitHub **pre-release**. It uses the repository's built-in `GITHUB_TOKEN`; no extra credentials are needed. Version tags must match the app version. Releases remain previews until runtime verification is complete.

To produce the same release assets locally, run `bash scripts/package-release.sh`. Nothing in the build/release workflow starts the app or accesses rekordbox settings.

### App artwork

The selected icon is a single centred, three-part white hexagon with mint sync arrows on charcoal. The shape is based on the supplied `rekordbox-vector-logo-2022.svg`, which is kept as the unmodified design reference.

- **Sync:** `Resources/AppIcon-sync.svg`, `.png`, `.icns` — the default app icon, with one hexagon.
- **Record:** `Resources/AppIcon-record.svg`, `.png`, `.icns` — the earlier overlapping-hexagon concept.
- **Side by side:** `Resources/Icon-Comparison.png`.
- **Selected icon:** `Resources/AppIcon.svg`, `.png` (1024px), `.icns` (Mac sizes including Retina).

The build script regenerates both styles from the shared vector geometry in `scripts/render-icon.swift` and embeds the selected one in the application. An ordinary build uses `sync`; the earlier concept remains available with `APP_ICON_STYLE=record bash scripts/build-app.sh`. Artwork rendering does not launch the app or touch rekordbox settings.

## Use (after testing is approved)

1. Set up your preferred options in rekordbox, then **quit rekordbox** so it writes them to disk.
2. Open the cloner. **Inspect** the settings folder and select the groups you want to transfer.
3. Name your setup and choose **Save setup…**. Take the `.rbsetup` file and the app ZIP to the other Mac.
4. On that Mac, open and quit rekordbox at least once. In the cloner, select its settings folder and the groups to import.
5. Choose **Open backup & preview…**. Review the changed preferences, replaced mapping/effects files and skipped items.
6. Choose **Save restore point & import**, then open rekordbox.
7. After the party, quit rekordbox and choose **Restore previous setup…** on the host Mac. Select the restore point from before your import.

Use the **same major rekordbox version** on both Macs; using the same exact version is preferable. Cross-major imports are blocked. Within a major version, unknown destination preference keys and absent effects files are skipped rather than guessed. Pad-editor files require matching `pad/Version` contents. MIDI mappings remain controller-specific, and software preferences cannot unlock licensed or hardware-dependent features.

### Copy Pad FX between decks

The **Copy Pad FX between decks** panel works on the selected Mac settings folder, independently of the backup group checkboxes:

1. Quit rekordbox, then choose **Deck 1 → Deck 2** or **Deck 2 → Deck 1**.
2. Select **Pad FX 1**, **Pad FX 2**, or **Both banks** (the default).
3. Choose **Preview copy…** to see which destination pads and parameters would change.
4. Choose **Save restore point & copy**. You can undo the copy with **Restore previous setup…**.

This copies all 16 stored slots per selected bank, including effect selections, parameters, colours and hold/inheritance settings. The source deck, decks 3/4 and unselected banks are preserved. Only `PadFxSettings.xml` is edited; saving a portable setup afterwards includes the copied banks when **Pad FX & effects** is selected.

Copying supports the inspected `PADFXINFO_500` XML format (deck numbers 1–4, bank indices 0–1). Incomplete banks or unrecognised formats are rejected. The app reports already-matching banks without writing files. This feature has been compiled but has not been runtime-tested.

## What transfers

| Group | Included |
| --- | --- |
| STEMS | Activation, **Prioritize speed / sound quality** (`PartAnalysisQuality`), increased memory, multithreading, 3/4-part mode, part layout, STEMS waveform display |
| Waveforms | Colour/style, zoom, beat counts, phrase/vocal display |
| Screen layout | Deck layout, panels, fonts/language, source visibility, playlist/search display, browser column order/width/visibility/sort, artwork display, selected track-suggestion criteria and editor controls |
| Deck & mixer | Quantize, cue/load behaviour, tempo ranges, vinyl mode, fader curves, Auto Play/Auto Mix, Smart Fader preferences, Mix Point Link and metronome |
| Sampler & sequencer | Slot count/layout/colours, load lock, capture/sequence behaviour, active bank, tempo, sync and quantize |
| Groove Circuit | Activation, capture stem, swap-slot load lock, slot-title display, visible banks, FX selections and beat parameters |
| Pad FX & effects | Pad FX banks/colours, FX units, Merge FX settings |
| Mappings | Keyboard presets and active preset, MIDI CSV mappings, custom pad layouts |
| Analysis preferences | Analysis mode, BPM/range, key/phrase/vocal/high-precision analysis and cue-generation options |
| Recording preferences | Start/stop triggers, silence thresholds, track splitting, normalization and track-info wizard |
| Controller feel & display | Jog display/LED preferences, slip indicators, backspin length, fader start and meter/display options |
| Video & lighting | Activation, video effect preferences/assignments, overlay size/transparency, lighting timing/quantize/fader behaviour |

All groups are independently selectable. Only the video/lighting group starts unchecked; all other groups, including controller preferences, sampler and STEMS processing, start checked. Hardware-specific preferences only apply where the connected equipment supports them.

The main `rekordbox3.settings` file contains **account tokens as well as preferences**. Portable backups export only explicitly approved scalar properties. Imports merge those properties into the target's existing XML. The whole main settings file is never included in a portable backup.

`PlaySettings.xml` is similarly filtered: saved playback state, cue positions and current tempo are not exported. Supported effects files and mappings are copied as complete files; same-named mappings replace their counterpart, and additional destination mappings are retained.

### Sampler, browser and other mixed-content files

- **`SamplerSettings1.xml`:** copies only the named bank/tempo/sync/quantize controls, both at the top level and inside `SamplerSet`. `Track` records, audio URLs, names, library IDs and per-track playback parameters stay on the destination. Sample files and assignments are not bundled.
- **`browseSetting.xml`:** copies approved scalar display controls and numeric table-column/artwork layouts. Playlists, selected library IDs, tree shortcuts, browsing history, related-track selections and palette assignments are not exported. A table layout is skipped if its column IDs or structure differ on the destination, preserving newer/unknown columns.
- **`GrooveCircuitSettings.xml`:** copies named bank/FX controls and validated numeric FX beat settings. Drum audio and library assignments are not copied.
- **`VideoSettings.xml`:** copies overlay size and transparency only, preserving any destination overlay media/text and other fields.
- **`EditSettings.xml`:** copies loop mode, tool-panel visibility and selected pad tab only.

These files are **merged property by property**, never copied wholesale into a portable backup. Browser/FX fragments use strict element/attribute allowlists. Unsupported source structures stop the backup with an explanation; incompatible destination structures appear in the import preview's skipped list.

### What stays local

Music, database files, playlists, existing analysis/cue data, accounts, licenses, cloud/service configuration, audio-device routing/buffer size, mixer/DVS routing and calibration, main output/microphone levels, sample assignments/files, recording locations and machine-specific paths are excluded. Analysis preferences affect future analysis; they do not transfer existing library results. Recording auto-import and operations that write tags/export/sync libraries are excluded. Source-visibility checkboxes do not transfer streaming logins.

Window coordinates/fullscreen, media/fixture assets, personal genre lists, device-export `.DAT` settings, and unknown/unreviewed properties also stay local. This remains an explicitly reviewed preference transfer, not a raw copy of every rekordbox file. The exact scalar/file allowlist is in `Sources/RekordboxSetupCloner/SettingsPolicy.swift`; nested XML validation is in `SettingsXML.swift`.

New backups use format version **2**. This app still reads version 1 backups and places their sampler preferences in the new sampler group. Use the updated app on both Macs to import version 2 backups. Older backups do not contain the newly added settings: save a fresh setup to include them.

## Restore points

Before an import, Pad FX copy, or restore, the app saves the **complete original contents of each affected file**, including absence markers for newly added mappings, in:

```text
~/Library/Application Support/Rekordbox Setup Cloner/Restore Points/
```

These `.rbrecovery` files stay local and can contain login credentials from the original main settings file. The directory uses owner-only permissions (`0700`), and files use `0600`; they are **not encrypted**. Transfer only the `.rbsetup` file to another person.

Restore points belong to the exact settings-folder path where they were created. Restoration replaces the affected files with their original bytes and removes mappings added by that import. If those files changed since the import, the app flags this before restoring: later edits in those files would also be undone. A new restore point is saved before restoration itself.

Writes are atomic per file, with rollback attempted on an ordinary write error. A multi-file import is not a filesystem-wide transaction: after a power loss or forced app termination, use the persisted restore point. Previewed files are rechecked before applying changes, and the app refuses backup/import/Pad FX copy/restore while it detects rekordbox running. Keep rekordbox closed throughout the operation.

## Verification awaiting approval

No application runs, automated tests, or live imports should be performed without the owner's approval. The next verification step should use synthetic settings in an isolated temporary folder, covering:

- Allowlisted export and preservation of account/library/audio-routing properties during merge.
- XML escaping and nested XML preservation.
- Major-version rejection, missing-key skipping and pad-version mismatch handling.
- Mapping addition/replacement and byte-for-byte restoration, including absent files.
- Stale-preview rejection, invalid archives, path traversal and symbolic links.
- Rollback after a partial write failure, and restoring after later preference edits.
- Pad FX copy in both directions and all bank selections; preservation of the source/other decks and unselected banks; rejection of missing/duplicate slots; no-op matching banks and restoration of the original Pad FX file.
- STEMS processing preferences and independent selection of the added groups.
- Sampler controls at both nesting levels while retaining destination Track records, library IDs, URLs and per-track parameters; rejection of attempts to include them in portable backups.
- Browser columns without tree/playlist/track references; schema/column mismatch skipping; matching layouts producing no edits.
- Numeric-only Groove Circuit fragments and video overlay controls while preserving unrelated destination fields.
- Combined scalar/structured edits in one file, exact local restoration of all new preference files, and version 1 backup compatibility.

Only after those checks and separate approval should a real rekordbox round-trip be tried.
