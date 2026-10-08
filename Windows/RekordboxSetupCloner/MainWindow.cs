using System.Runtime.InteropServices;

namespace RekordboxSetupCloner;

internal sealed class MainWindow(SettingsStore? settingsStore = null, string? settingsDirectory = null) : NativeWindow
{
    private const int ChooseId = 10, InspectId = 11, AllId = 12, NoneId = 13, SaveId = 14,
        ImportId = 15, RestoreId = 16, CopyId = 17, RevealId = 18, GroupsId = 40, DirectionId = 50, BanksId = 60;
    private readonly SettingsStore store = settingsStore ?? new();
    private readonly Dictionary<SettingsGroup, int> counts = [];
    private readonly nint[] directions = new nint[2], bankButtons = new nint[3];
    private nint title, introduction, folderLabel, folder, choose, inspect, version, groupsLabel, all, none, groupCount,
        name, save, import, restore, padLabel, copy, localNote, status, reveal, folderHint, groupList,
        saveLabel, saveDetail, importLabel, importDetail, padDetail, directionLabel, banksLabel;
    private nint artwork;
    private int sourceDeck = 1, bankChoice;
    private string? lastOutput;
    protected override string Title => "Rekordbox Setup Cloner";
    protected override int InitialWidth => 1000;
    protected override int InitialHeight => 890;
    protected override int MinimumHeight => 480;
    protected override int ContentHeight => 980;

    protected override void CreateControls()
    {
        title = Heading("Your decks. Your setup.");
        introduction = StyledLabel("Bring your rekordbox preferences to another computer.", ink: WindowTheme.Muted);
        artwork = Native.LoadImageW(Native.GetModuleHandleW(null), 32512, 1, Pixels(64), Pixels(64), 0x8000);
        folderLabel = StyledLabel("Settings on this PC", UiFont.Strong, onSurface: true);
        folder = Control("EDIT", settingsDirectory ?? SettingsStore.DefaultDirectory, 30, Native.WS_TABSTOP | 0x880);
        Style(folder, UiFont.Mono, onSurface: true);
        choose = Button("Choose…", ChooseId);
        inspect = Button("Inspect", InspectId);
        version = StyledLabel("Not inspected", UiFont.Caption, WindowTheme.Muted, true);
        folderHint = StyledLabel("rekordbox 7 also stores settings in the folder named rekordbox6.", UiFont.Caption, WindowTheme.Muted, true);
        groupsLabel = StyledLabel("WHAT TO TRANSFER", UiFont.Caption, WindowTheme.Muted);
        all = Button("All", AllId, ButtonKind.Link, false);
        none = Button("None", NoneId, ButtonKind.Link, false);
        groupList = Control("LISTBOX", "Preference groups", GroupsId, Native.WS_TABSTOP | Native.WS_VSCROLL | 0x159);
        foreach (var group in GroupInfo.All)
        {
            var text = Marshal.StringToHGlobalUni(group.Title() + ". " + group.Detail());
            try { Native.SendMessageW(groupList, 0x180, 0, text); }
            finally { Marshal.FreeHGlobal(text); }
            Native.SendMessageW(groupList, 0x185, group == SettingsGroup.visuals ? 0u : 1u, (nint)(int)group);
        }
        Native.SendMessageW(groupList, 0x197, 0, 0); // Start at STEMS, rather than the last selected group.
        Native.SendMessageW(groupList, 0x19E, 0, 0);
        groupCount = StyledLabel("", UiFont.Caption, WindowTheme.Muted);
        saveLabel = StyledLabel("↑  Save your setup", UiFont.Section, onSurface: true);
        saveDetail = StyledLabel("Create a portable backup of the selected preferences.", ink: WindowTheme.Muted, onSurface: true);
        name = Edit("My party setup", 31);
        save = Button("Save setup…", SaveId, ButtonKind.Primary);
        importLabel = StyledLabel("↓  Use a saved setup", UiFont.Section, onSurface: true);
        importDetail = StyledLabel("Preview changes for the selected groups. Importing saves this PC’s current settings first.", ink: WindowTheme.Muted, onSurface: true);
        import = Button("Open backup & preview…", ImportId);
        restore = Button("Restore previous setup…", RestoreId, onSurface: false);
        padLabel = StyledLabel("▣  Copy Pad FX between decks", UiFont.Section, onSurface: true);
        padDetail = StyledLabel("Copy a deck’s Pad FX assignments and parameters in the selected settings folder.", ink: WindowTheme.Muted, onSurface: true);
        directionLabel = StyledLabel("Direction", UiFont.Caption, WindowTheme.Muted, true);
        banksLabel = StyledLabel("Banks", UiFont.Caption, WindowTheme.Muted, true);
        directions[0] = Button("Deck 1 → Deck 2", DirectionId, ButtonKind.Segment);
        directions[1] = Button("Deck 2 → Deck 1", DirectionId + 1, ButtonKind.Segment);
        bankButtons[0] = Button("Both banks", BanksId, ButtonKind.Segment);
        bankButtons[1] = Button("Pad FX 1", BanksId + 1, ButtonKind.Segment);
        bankButtons[2] = Button("Pad FX 2", BanksId + 2, ButtonKind.Segment);
        UpdateSegments();
        copy = Button("Preview copy…", CopyId);
        localNote = StyledLabel("Music, playlists, login details and audio-device routing stay on their own computer.", UiFont.Caption, WindowTheme.Muted);
        status = StyledLabel("Choose your settings folder, then inspect it to get started.", ink: WindowTheme.Muted);
        reveal = Button("Show in Explorer", RevealId, ButtonKind.Link, false);
        Native.ShowWindow(reveal, 0);
        UpdateSelection();
    }

    private void UpdateSegments()
    {
        for (var i = 0; i < directions.Length; i++) SelectSegment(directions[i], sourceDeck == i + 1);
        for (var i = 0; i < bankButtons.Length; i++) SelectSegment(bankButtons[i], bankChoice == i);
    }
    protected override void Layout(int width, int height)
    {
        if (title == 0) return;
        var inner = width - 56;
        var left = inner - 320;
        var right = width - 323;
        Place(title, 108, 26, inner - 260, 40);
        Place(introduction, 108, 73, inner - 180, 22);
        Place(folderLabel, 46, 132, inner - 330, 22);
        Place(version, width - 360, 135, 310, 20);
        Place(folder, 46, 171, inner - 216, 24);
        Place(choose, width - 232, 164, 86, 34);
        Place(inspect, width - 136, 164, 86, 34);
        Place(folderHint, 46, 204, inner - 36, 18);
        Place(groupsLabel, 28, 254, left - 126, 20);
        Place(all, 28 + left - 110, 246, 48, 30);
        Place(none, 28 + left - 56, 246, 56, 30);
        Place(groupList, 28, 284, left, 350);
        if (Native.SendMessageW(groupList, 0x1A1, 0, 0) != Pixels(78))
            Native.SendMessageW(groupList, 0x1A0, 0, (nint)Pixels(78));
        Place(groupCount, 28, 647, left, 22);
        Place(saveLabel, right + 20, 269, 255, 25);
        Place(saveDetail, right + 20, 307, 255, 42);
        Place(name, right + 20, 365, 255, 31);
        Place(save, right + 20, 408, 255, 38);
        Place(importLabel, right + 20, 499, 255, 25);
        Place(importDetail, right + 20, 537, 255, 62);
        Place(import, right + 20, 613, 255, 38);
        Place(padLabel, 46, 702, inner - 36, 26);
        Place(padDetail, 46, 740, inner - 36, 23);
        Place(directionLabel, 46, 778, 275, 18);
        const int segmentWidth = 137;
        var bankX = 46 + segmentWidth * 2 + 18;
        Place(directions[0], 46, 800, segmentWidth, 34);
        Place(directions[1], 46 + segmentWidth, 800, segmentWidth, 34);
        Place(banksLabel, bankX, 778, 267, 18);
        for (var i = 0; i < 3; i++) Place(bankButtons[i], bankX + i * 84, 800, 84, 34);
        Place(copy, width - 189, 800, 139, 34);
        Place(localNote, 28, 885, inner, 20);
        Place(status, 28, 923, inner - (lastOutput == null ? 228 : 370), 42);
        Place(reveal, width - 390, 923, 135, 34);
        Place(restore, width - 247, 920, 219, 38);
    }
    protected override void Paint(nint dc, int width, int height)
    {
        var inner = width - 56;
        if (artwork != 0) Native.DrawIconEx(dc, Pixels(28), Bounds(0, 28, 0, 0).Top, artwork, Pixels(64), Pixels(64), 0, 0, 3);
        Theme.Box(dc, Bounds(width - 198, 40, 170, 34), WindowTheme.AccentSoft, WindowTheme.AccentSoft, 9);
        Theme.Text(dc, "☷  Preferences only", Bounds(width - 198, 40, 170, 34), UiFont.Strong, WindowTheme.Accent, 0x25);
        Theme.Box(dc, Bounds(28, 114, inner, 122), WindowTheme.Surface, WindowTheme.Border, 12);
        Theme.Box(dc, Bounds(width - 323, 250, 295, 214), WindowTheme.Surface, WindowTheme.Border, 12);
        Theme.Box(dc, Bounds(width - 323, 480, 295, 189), WindowTheme.Surface, WindowTheme.Border, 12);
        Theme.Box(dc, Bounds(28, 684, inner, 166), WindowTheme.Surface, WindowTheme.Border, 12);
        var line = Bounds(28, 866, inner, 0);
        Theme.Line(dc, line.Left, line.Top, line.Right, line.Top);
    }
    protected override bool DrawItem(Native.DrawItem item)
    {
        if (item.ControlId != GroupsId || item.ItemId >= GroupInfo.All.Length) return false;
        var group = GroupInfo.All[item.ItemId];
        var selected = (item.State & 1) != 0;
        var bounds = item.Bounds;
        Theme.Fill(item.DC, bounds, WindowTheme.Background);
        bounds.Right -= Pixels(6); bounds.Bottom -= Pixels(7);
        Theme.Box(item.DC, bounds, WindowTheme.Surface, selected ? 0xCBDCFBu : WindowTheme.Border, 9);
        var check = new Native.Rect { Left = bounds.Left + Pixels(14), Top = bounds.Top + Pixels(16), Right = bounds.Left + Pixels(30), Bottom = bounds.Top + Pixels(32) };
        Theme.Box(item.DC, check, selected ? WindowTheme.Accent : WindowTheme.Surface, selected ? WindowTheme.Accent : WindowTheme.Border, 4);
        if (selected)
        {
            Theme.Line(item.DC, check.Left + Pixels(4), check.Top + Pixels(8), check.Left + Pixels(7), check.Top + Pixels(11), WindowTheme.Surface, 2);
            Theme.Line(item.DC, check.Left + Pixels(7), check.Top + Pixels(11), check.Left + Pixels(12), check.Top + Pixels(5), WindowTheme.Surface, 2);
        }
        var glyphs = new[] { "\uE8D6", "\uE9D9", "\uE7F4", "\uE9E9", "\uE8F1", "\uE8D7", "\uE734", "\uE765", "\uE9D9", "\uE714", "\uE7FC", "\uE714" };
        var iconBounds = new Native.Rect { Left = bounds.Left + Pixels(42), Top = bounds.Top + Pixels(14), Right = bounds.Left + Pixels(65), Bottom = bounds.Top + Pixels(37) };
        Theme.Text(item.DC, glyphs[(int)group], iconBounds, UiFont.Icon, WindowTheme.Accent, 0x25);
        var textBounds = new Native.Rect { Left = bounds.Left + Pixels(77), Top = bounds.Top + Pixels(12), Right = bounds.Right - Pixels(43), Bottom = bounds.Top + Pixels(33) };
        Theme.Text(item.DC, group.Title(), textBounds, UiFont.Strong, flags: 0x8020);
        textBounds.Top = bounds.Top + Pixels(36); textBounds.Bottom = bounds.Bottom - Pixels(5); textBounds.Right = bounds.Right - Pixels(12);
        Theme.Text(item.DC, group.Detail(), textBounds, UiFont.Caption, WindowTheme.Muted, 0x10);
        if (counts.TryGetValue(group, out var count))
        {
            textBounds = new Native.Rect { Left = bounds.Right - Pixels(41), Top = bounds.Top + Pixels(14), Right = bounds.Right - Pixels(12), Bottom = bounds.Top + Pixels(33) };
            Theme.Text(item.DC, count.ToString(), textBounds, UiFont.Caption, WindowTheme.Muted, 0x22);
        }
        if ((item.State & 0x10) != 0 && (item.State & 0x200) == 0) Native.DrawFocusRect(item.DC, ref bounds);
        return true;
    }
    protected override void Command(int id, int notification)
    {
        if (id == GroupsId && notification == 1) { UpdateSelection(); return; }
        if (notification != 0) return;
        if (id is >= DirectionId and < DirectionId + 2) { sourceDeck = id - DirectionId + 1; UpdateSegments(); return; }
        if (id is >= BanksId and < BanksId + 3) { bankChoice = id - BanksId; UpdateSegments(); return; }
        Run(() =>
        {
            switch (id)
            {
                case ChooseId:
                    if (NativeDialogs.Folder(Handle, Text(folder)) is { } selected) { SetText(folder, selected); Inspect(); }
                    break;
                case InspectId: Inspect(); break;
                case AllId: SelectGroups(true); break;
                case NoneId: SelectGroups(false); break;
                case SaveId: Save(); break;
                case ImportId: Import(); break;
                case RestoreId: Restore(); break;
                case CopyId: Copy(); break;
                case RevealId: Reveal(); break;
            }
        });
    }
    private HashSet<SettingsGroup> SelectedGroups => GroupInfo.All.Where(group => Native.SendMessageW(groupList, 0x187, (nuint)(int)group, 0) > 0).ToHashSet();
    private void SelectGroups(bool selected)
    {
        Native.SendMessageW(groupList, 0x185, selected ? 1u : 0u, -1);
        UpdateSelection();
    }
    private void UpdateSelection()
    {
        var count = SelectedGroups.Count;
        SetText(groupCount, $"{count} of {GroupInfo.All.Length} groups selected. Scroll for all groups.");
        Native.EnableWindow(save, count > 0);
        Native.EnableWindow(import, count > 0);
    }
    private void Inspect()
    {
        SetText(version, "");
        var found = store.Inspect(Text(folder));
        SetText(version, found.Version.Label + (found.StemsEnabled is { } enabled ? enabled ? " · STEMS on" : " · STEMS off" : ""));
        counts.Clear();
        foreach (var (group, count) in found.Counts) counts[group] = count;
        Native.InvalidateRect(groupList, 0, false);
        SetText(status, "Settings found. Quit rekordbox before saving or importing a setup.");
    }
    private void Output(string path, string message)
    {
        lastOutput = path;
        SetText(status, message);
        Native.ShowWindow(reveal, 5);
        RefreshLayout();
    }
    private void Save()
    {
        var directory = Text(folder);
        var profile = store.Capture(directory, Text(name), SelectedGroups);
        var filename = string.Concat(profile.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).TrimEnd(' ', '.');
        if (!SettingsPolicy.ValidRelativePath(filename)) filename = "My setup";
        var path = NativeDialogs.SaveFile(Handle, "Save portable setup", "rekordbox setup (*.rbsetup)\0*.rbsetup\0\0", filename + ".rbsetup", "rbsetup");
        if (path == null) return;
        store.SaveProfile(profile, path, directory);
        Output(path, $"Saved {profile.PreferenceCount} preferences and {profile.Files.Count} settings files to {Path.GetFileName(path)}.");
    }
    private void Import()
    {
        var path = NativeDialogs.OpenFile(Handle, "Open a saved setup", "rekordbox setup (*.rbsetup;*.json)\0*.rbsetup;*.json\0All files (*.*)\0*.*\0\0");
        if (path == null) return;
        var plan = store.Plan(store.LoadProfile(path), Text(folder), SelectedGroups);
        var description = $"Saved {plan.Profile.CreatedAt.ToLocalTime():g} · {plan.Profile.RekordboxVersion.Label}\nDestination: {plan.Destination}\n{plan.Changes.Count} changes across {plan.Edits.Count} files";
        if (!PreviewWindow.Confirm(Handle, $"Preview “{plan.Profile.Name}”", description, plan.Changes, plan.Skipped,
            "Import saves a private, local restore point before changing files. Keep rekordbox closed until it finishes.",
            "Save restore point & import", plan.Edits.Count > 0, plan.CompatibilityNotice)) return;
        Output(store.Apply(plan), $"Imported “{plan.Profile.Name}”. Your previous setup is saved locally. You can now open rekordbox.");
    }
    private void Copy()
    {
        var source = sourceDeck;
        var selected = bankChoice;
        int[] modes = selected switch { 1 => [0], 2 => [1], _ => [0, 1] };
        var bankTitle = selected switch { 1 => "Pad FX 1", 2 => "Pad FX 2", _ => "Both banks" };
        var plan = store.PlanPadFXCopy(Text(folder), source, 3 - source, modes);
        if (!PreviewWindow.Confirm(Handle, $"Copy Pad FX: Deck {source} → Deck {plan.TargetDeck}",
            $"{bankTitle} · {plan.Version.Label}\nDestination: {plan.Destination}\n{plan.Changes.Count} pad slots will change on deck {plan.TargetDeck}", plan.Changes, [],
            "Copy replaces the selected banks and saves a local restore point first. Keep rekordbox closed until it finishes.",
            "Save restore point & copy", plan.Edits.Count > 0)) return;
        Output(store.Apply(plan), $"Copied Pad FX: Deck {source} → Deck {plan.TargetDeck} ({bankTitle}). Previous settings saved locally.");
    }
    private void Restore()
    {
        var path = NativeDialogs.OpenFile(Handle, "Restore this PC’s previous setup", "Local restore point (*.rbrecovery;*.json)\0*.rbrecovery;*.json\0\0", store.RecoveryDirectory);
        if (path == null) return;
        var directory = Text(folder);
        var archive = store.LoadRecovery(path, directory);
        var plan = store.RecoveryEdits(archive, directory);
        if (plan.Edits.Count == 0) { SetText(status, "This setup is already restored."); return; }
        var changes = plan.Edits.Select(edit => new Change(SettingsPolicy.FileGroup(edit.Path) ?? SettingsGroup.layout, edit.Path,
            plan.Changed.Contains(edit.Path) ? "Has later changes — these will also be undone" : "Restore original file contents")).ToList();
        var note = "The current files will be saved in a new local restore point first. Keep rekordbox closed until restoration finishes.";
        if (plan.Changed.Count > 0) note += $"\n{plan.Changed.Count} files have later changes. Restoring also undoes later edits to other preferences in those files.";
        if (!PreviewWindow.Confirm(Handle, $"Restore the setup from {archive.CreatedAt.ToLocalTime():g}?",
            $"Restore point: {archive.ProfileName}\nDestination: {directory}\nRestore {plan.Edits.Count} complete settings files", changes, [], note, "Restore setup", true)) return;
        Output(store.Restore(plan.Edits, directory), "Previous setup restored. You can now open rekordbox.");
    }
    private void Reveal()
    {
        if (lastOutput == null) return;
        var result = Native.ShellExecuteW(Handle, "open", "explorer.exe", $"/select,\"{lastOutput}\"", null, 1);
        if (result <= 32) throw new SetupException("Could not open Explorer.");
    }
}
