using System.Diagnostics;
using System.Reflection;

namespace RekordboxSetupCloner;

internal sealed class MainForm : Form
{
    private readonly SettingsStore store = new();
    private readonly TextBox directory = new() { ReadOnly = true, Dock = DockStyle.Fill, Text = SettingsStore.DefaultDirectory, AccessibleName = "rekordbox settings folder" };
    private readonly TextBox profileName = new() { Dock = DockStyle.Fill, Text = "My party setup", AccessibleName = "Setup name", MaxLength = 120 };
    private readonly Label inspection = TextLabel("Choose your settings folder, then inspect it to get started.");
    private readonly Label status = TextLabel("Quit rekordbox before saving, importing, copying Pad FX, or restoring settings.");
    private readonly Dictionary<SettingsGroup, CheckBox> groupChecks = [];
    private readonly Dictionary<SettingsGroup, Label> groupLabels = [];
    private readonly ComboBox direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, AccessibleName = "Copy direction" };
    private readonly ComboBox banks = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, AccessibleName = "Pad FX banks" };
    private readonly Button reveal;
    private readonly Button save;
    private readonly Button import;
    private string? lastOutput;

    public MainForm()
    {
        Text = "Rekordbox Setup Cloner";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(860, 700);
        ClientSize = new Size(980, 840);
        StartPosition = FormStartPosition.CenterScreen;
        if (Environment.ProcessPath is { } executable) Icon = Icon.ExtractAssociatedIcon(executable);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
        Controls.Add(scroll);
        var content = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(content);
        void Add(Control control) { control.Dock = DockStyle.Top; control.Margin = new Padding(0, 0, 0, 18); content.Controls.Add(control); }

        var header = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        using var art = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png");
        if (art != null)
        {
            using var image = Image.FromStream(art);
            header.Controls.Add(new PictureBox { Image = new Bitmap(image), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "App icon" }, 0, 0);
        }
        var heading = Stack();
        heading.Controls.Add(TextLabel("Your decks. Your setup.", 22, FontStyle.Bold));
        heading.Controls.Add(TextLabel("Bring your rekordbox preferences to another computer."));
        header.Controls.Add(heading, 1, 0);
        Add(header);

        var folder = Stack();
        folder.Controls.Add(TextLabel("Settings on this PC", 12, FontStyle.Bold));
        var folderRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3 };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.Controls.Add(directory);
        folderRow.Controls.Add(Button("Choose…", ChooseDirectory));
        folderRow.Controls.Add(Button("Inspect", Inspect));
        folder.Controls.Add(folderRow);
        folder.Controls.Add(TextLabel(@"Default: %APPDATA%\Pioneer\rekordbox", 9));
        folder.Controls.Add(inspection);
        Add(folder);

        var middle = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        var groups = Stack();
        var groupHeader = Row();
        groupHeader.Controls.Add(TextLabel("WHAT TO TRANSFER", 10, FontStyle.Bold));
        groupHeader.Controls.Add(Button("All", () => SelectGroups(true)));
        groupHeader.Controls.Add(Button("None", () => SelectGroups(false)));
        groups.Controls.Add(groupHeader);
        var groupScroll = new Panel { Dock = DockStyle.Top, AutoScroll = true, Height = 370, BorderStyle = BorderStyle.FixedSingle };
        var groupStack = Stack();
        foreach (var group in GroupInfo.All)
        {
            var check = new CheckBox { Text = group.Title(), UseMnemonic = false, Checked = group != SettingsGroup.visuals, AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            check.CheckedChanged += (_, _) => UpdateActions();
            groupChecks[group] = check;
            var detail = TextLabel(group.Detail(), 9);
            detail.Margin = new Padding(24, 0, 12, 12);
            groupLabels[group] = detail;
            groupStack.Controls.Add(check);
            groupStack.Controls.Add(detail);
        }
        groupScroll.Controls.Add(groupStack);
        groups.Controls.Add(groupScroll);
        middle.Controls.Add(groups, 0, 0);

        var actions = Stack();
        actions.Padding = new Padding(20, 0, 0, 0);
        actions.Controls.Add(TextLabel("Save your setup", 14, FontStyle.Bold));
        actions.Controls.Add(TextLabel("Create a portable backup of the selected preferences."));
        actions.Controls.Add(profileName);
        save = Button("Save setup…", Save);
        save.Dock = DockStyle.Top;
        save.BackColor = Color.FromArgb(199, 240, 224);
        actions.Controls.Add(save);
        var importTitle = TextLabel("Use a saved setup", 14, FontStyle.Bold);
        importTitle.Margin = new Padding(3, 28, 3, 6);
        actions.Controls.Add(importTitle);
        actions.Controls.Add(TextLabel("Preview changes for the selected groups. Importing saves this PC’s current settings first."));
        import = Button("Open backup & preview…", PreviewImport);
        import.Dock = DockStyle.Top;
        actions.Controls.Add(import);
        middle.Controls.Add(actions, 1, 0);
        Add(middle);

        var pad = Stack();
        pad.Controls.Add(TextLabel("Copy Pad FX between decks", 14, FontStyle.Bold));
        pad.Controls.Add(TextLabel("Copy a deck’s Pad FX assignments and parameters in the selected settings folder."));
        direction.Items.AddRange(["Deck 1 → Deck 2", "Deck 2 → Deck 1"]);
        direction.SelectedIndex = 0;
        banks.Items.AddRange(["Both banks", "Pad FX 1", "Pad FX 2"]);
        banks.SelectedIndex = 0;
        var padRow = Row();
        padRow.Controls.Add(TextLabel("Direction:"));
        padRow.Controls.Add(direction);
        padRow.Controls.Add(TextLabel("Banks:"));
        padRow.Controls.Add(banks);
        padRow.Controls.Add(Button("Preview copy…", PreviewPadFXCopy));
        pad.Controls.Add(padRow);
        Add(pad);

        Add(TextLabel("Music, playlists, login details and audio-device routing stay on their own computer.", 9));
        Add(status);
        var footer = Row();
        reveal = Button("Show in Explorer", RevealOutput);
        reveal.Enabled = false;
        footer.Controls.Add(reveal);
        footer.Controls.Add(Button("Restore previous setup…", Restore));
        Add(footer);
    }

    private static TableLayoutPanel Stack()
    {
        var panel = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }
    private static FlowLayoutPanel Row() => new() { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
    private static Label TextLabel(string text, float size = 10, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, UseMnemonic = false, AutoSize = true, Dock = DockStyle.Top, Font = new Font("Segoe UI", size, style), Margin = new Padding(3, 3, 3, 8)
    };
    private Button Button(string text, Action action)
    {
        var button = new Button { Text = text, UseMnemonic = false, AutoSize = true, Padding = new Padding(8, 4, 8, 4), Margin = new Padding(3, 3, 8, 8) };
        button.Click += (_, _) => Run(action);
        return button;
    }
    private void Run(Action action)
    {
        try { UseWaitCursor = true; action(); }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Couldn’t complete the operation", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { UseWaitCursor = false; }
    }
    private HashSet<SettingsGroup> SelectedGroups => groupChecks.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToHashSet();
    private void UpdateActions() { if (save != null) save.Enabled = import.Enabled = SelectedGroups.Count > 0; }
    private void SelectGroups(bool selected) { foreach (var check in groupChecks.Values) check.Checked = selected; }

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the rekordbox settings folder", UseDescriptionForTitle = true, SelectedPath = directory.Text, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        directory.Text = dialog.SelectedPath;
        inspection.Text = "";
        Inspect();
    }
    private void Inspect()
    {
        inspection.Text = "";
        var found = store.Inspect(directory.Text);
        inspection.Text = $"rekordbox {found.Version.Display}" + (found.StemsEnabled is { } enabled ? enabled ? " · STEMS on" : " · STEMS off" : "");
        foreach (var group in GroupInfo.All)
            groupLabels[group].Text = group.Detail() + (found.Counts.TryGetValue(group, out var count) ? $"  ({count} items found)" : "");
        status.Text = "Settings found. Quit rekordbox before saving or importing a setup.";
    }
    private void Output(string path, string message)
    {
        lastOutput = path;
        reveal.Enabled = true;
        status.Text = message;
    }
    private void Save()
    {
        var profile = store.Capture(directory.Text, profileName.Text, SelectedGroups);
        var name = string.Concat(profile.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).TrimEnd(' ', '.');
        if (!SettingsPolicy.ValidRelativePath(name)) name = "My setup";
        using var dialog = new SaveFileDialog { Title = "Save portable setup", Filter = "rekordbox setup (*.rbsetup)|*.rbsetup", DefaultExt = "rbsetup", FileName = name + ".rbsetup", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        store.SaveProfile(profile, dialog.FileName, directory.Text);
        Output(dialog.FileName, $"Saved {profile.PreferenceCount} preferences and {profile.Files.Count} settings files to {Path.GetFileName(dialog.FileName)}.");
    }
    private void PreviewImport()
    {
        using var dialog = new OpenFileDialog { Title = "Open a saved setup", Filter = "rekordbox setup (*.rbsetup;*.json)|*.rbsetup;*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var profile = store.LoadProfile(dialog.FileName);
        var plan = store.Plan(profile, directory.Text, SelectedGroups);
        var description = $"Saved {profile.CreatedAt.ToLocalTime():g} · rekordbox {profile.RekordboxVersion.Display}\nDestination: {plan.Destination}\n{plan.Changes.Count} changes across {plan.Edits.Count} files";
        if (!profile.RekordboxVersion.Components!.SequenceEqual(plan.TargetVersion.Components!))
            description += $"\nThis PC uses {plan.TargetVersion.Display}. Only recognised preferences and compatible settings files are included.";
        using var preview = new PreviewForm($"Preview “{profile.Name}”", description, plan.Changes, plan.Skipped,
            "Import saves a private, local restore point before changing files. Keep rekordbox closed until the import finishes.", "Save restore point & import", plan.Edits.Count > 0);
        if (preview.ShowDialog(this) != DialogResult.OK) return;
        var recovery = store.Apply(plan);
        Output(recovery, $"Imported “{profile.Name}”. Your previous setup is saved locally. You can now open rekordbox.");
    }
    private void PreviewPadFXCopy()
    {
        var source = direction.SelectedIndex == 0 ? 1 : 2;
        int[] modes = banks.SelectedIndex switch { 1 => [0], 2 => [1], _ => [0, 1] };
        var plan = store.PlanPadFXCopy(directory.Text, source, 3 - source, modes);
        using var preview = new PreviewForm($"Copy Pad FX: Deck {source} → Deck {3 - source}",
            $"{banks.SelectedItem} · rekordbox {plan.Version.Display}\nDestination: {plan.Destination}\n{plan.Changes.Count} pad slots will change on deck {plan.TargetDeck}",
            plan.Changes, [], $"Copy replaces the selected banks on deck {plan.TargetDeck} and saves a local restore point first. Keep rekordbox closed until it finishes.",
            "Save restore point & copy", plan.Edits.Count > 0);
        if (preview.ShowDialog(this) != DialogResult.OK) return;
        Output(store.Apply(plan), $"Copied Pad FX: Deck {source} → Deck {plan.TargetDeck} ({banks.SelectedItem}). Previous settings saved in a local restore point.");
    }
    private void Restore()
    {
        using var dialog = new OpenFileDialog { Title = "Restore this PC’s previous setup", InitialDirectory = store.RecoveryDirectory, Filter = "Local restore point (*.rbrecovery;*.json)|*.rbrecovery;*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var archive = store.LoadRecovery(dialog.FileName, directory.Text);
        var plan = store.RecoveryEdits(archive, directory.Text);
        if (plan.Edits.Count == 0) { status.Text = "This setup is already restored."; return; }
        var changes = plan.Edits.Select(e => new Change(SettingsPolicy.FileGroup(e.Path) ?? SettingsGroup.layout, e.Path,
            plan.Changed.Contains(e.Path) ? "Has later changes — these will also be undone" : "Restore original file contents")).ToList();
        var note = "The current files will be saved in a new local restore point first. Keep rekordbox closed until restoration finishes.";
        if (plan.Changed.Count > 0) note += $"\n{plan.Changed.Count} files have later changes. Restoring also undoes later edits to other preferences in those files.";
        using var preview = new PreviewForm($"Restore the setup from {archive.CreatedAt.ToLocalTime():g}?",
            $"Restore point: {archive.ProfileName}\nDestination: {directory.Text}\nRestore {plan.Edits.Count} complete settings files", changes, [], note, "Restore setup", true);
        if (preview.ShowDialog(this) != DialogResult.OK) return;
        Output(store.Restore(plan.Edits, directory.Text), "Previous setup restored. You can now open rekordbox.");
    }
    private void RevealOutput()
    {
        if (lastOutput != null) Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{lastOutput}\"", UseShellExecute = true });
    }
}
