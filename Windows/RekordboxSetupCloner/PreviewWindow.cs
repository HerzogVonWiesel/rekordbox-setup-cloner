using System.Runtime.InteropServices;

namespace RekordboxSetupCloner;

internal sealed class PreviewWindow(string title, string description, List<Change> changes, List<string> skipped,
    string note, string applyTitle, bool canApply, string? warning) : NativeWindow
{
    private nint headingControl, descriptionControl, warningControl, reportControl, noteControl, apply, cancel;
    private readonly List<PreviewRow> rows = BuildRows(changes, skipped);
    private sealed record PreviewRow(string Title, string Detail, bool Header = false);
    private bool accepted;
    protected override string Title => title;
    protected override int InitialWidth => 820;
    protected override int InitialHeight => 750;
    protected override int MinimumWidth => 680;
    protected override int MinimumHeight => 540;

    internal static bool Confirm(nint owner, string title, string description, List<Change> changes, List<string> skipped,
        string note, string applyTitle, bool canApply, string? warning = null)
    {
        var window = new PreviewWindow(title, description, changes, skipped, note, applyTitle, canApply, warning);
        Native.EnableWindow(owner, false);
        try
        {
            window.Open(owner);
            window.RunMessageLoop();
            return window.accepted;
        }
        finally
        {
            Native.EnableWindow(owner, true);
            Native.SetForegroundWindow(owner);
            Native.SetFocus(owner);
        }
    }
    protected override void CreateControls()
    {
        headingControl = StyledLabel(title, UiFont.Section);
        descriptionControl = StyledLabel(description.Replace("\n", "\r\n"), ink: WindowTheme.Muted);
        if (warning != null)
        {
            warningControl = StyledLabel("Note: " + warning, UiFont.Caption, WindowTheme.Warning);
            Background(warningControl, WindowTheme.WarningSoft);
        }
        reportControl = Control("LISTBOX", "Changes to apply", 10, Native.WS_TABSTOP | Native.WS_VSCROLL | 0x160);
        for (var i = 0; i < rows.Count; i++)
        {
            var text = Marshal.StringToHGlobalUni(rows[i].Title + ". " + rows[i].Detail);
            try { Native.SendMessageW(reportControl, 0x180, 0, text); }
            finally { Marshal.FreeHGlobal(text); }
        }
        noteControl = StyledLabel(note.Replace("\n", "\r\n"), UiFont.Caption, WindowTheme.Muted);
        apply = Button(applyTitle, 1, ButtonKind.Primary, false);
        cancel = Button("Cancel", 2, onSurface: false);
        Native.EnableWindow(apply, canApply);
        Native.SetFocus(cancel);
    }
    private static List<PreviewRow> BuildRows(List<Change> changes, List<string> skipped)
    {
        var result = new List<PreviewRow>();
        if (changes.Count == 0) result.Add(new("No changes to apply", "The selected settings already match, or none are compatible."));
        foreach (var group in GroupInfo.All)
        {
            var matching = changes.Where(change => change.Group == group).ToArray();
            if (matching.Length == 0) continue;
            result.Add(new(group.Title(), $"{matching.Length} changes", true));
            foreach (var change in matching) result.Add(new(change.Title, change.Detail));
        }
        if (skipped.Count > 0)
        {
            result.Add(new($"Skipped ({skipped.Count})", "These settings will stay as they are.", true));
            foreach (var reason in skipped) result.Add(new("Setting skipped", reason));
        }
        return result;
    }
    protected override void Layout(int width, int height)
    {
        if (descriptionControl == 0) return;
        var inner = width - 48;
        Place(headingControl, 24, 22, inner, 30);
        Place(descriptionControl, 24, 64, inner, 76);
        var top = 146;
        if (warningControl != 0) { Place(warningControl, 38, 157, inner - 28, 46); top = 224; }
        Place(reportControl, 24, top, inner, height - top - 146);
        for (var i = 0; i < rows.Count; i++)
        {
            var rowHeight = Pixels(rows[i].Header ? 47 : 80);
            if (Native.SendMessageW(reportControl, 0x1A1, (nuint)i, 0) != rowHeight)
                Native.SendMessageW(reportControl, 0x1A0, (nuint)i, (nint)rowHeight);
        }
        Place(noteControl, 24, height - 126, inner, 63);
        Place(cancel, 24, height - 55, 90, 34);
        Place(apply, width - 294, height - 55, 270, 34);
    }
    protected override void Paint(nint dc, int width, int height)
    {
        if (warningControl != 0) Theme.Box(dc, Bounds(24, 146, width - 48, 66), WindowTheme.WarningSoft, WindowTheme.WarningSoft, 9);
        var line = Bounds(24, height - 139, width - 48, 0);
        Theme.Line(dc, line.Left, line.Top, line.Right, line.Top);
    }
    protected override bool DrawItem(Native.DrawItem item)
    {
        if (item.ControlId != 10 || item.ItemId >= rows.Count) return false;
        var row = rows[(int)item.ItemId];
        var bounds = item.Bounds;
        Theme.Fill(item.DC, bounds, WindowTheme.Background);
        bounds.Right -= Pixels(7);
        if (row.Header)
        {
            var heading = bounds;
            heading.Top += Pixels(15); heading.Right -= Pixels(110);
            Theme.Text(item.DC, row.Title, heading, UiFont.Strong, flags: 0x8020);
            heading.Left = bounds.Right - Pixels(150); heading.Right = bounds.Right;
            Theme.Text(item.DC, row.Detail, heading, UiFont.Caption, WindowTheme.Muted, 0x22);
        }
        else
        {
            bounds.Bottom -= Pixels(8);
            Theme.Box(item.DC, bounds, WindowTheme.Surface, WindowTheme.Border, 9);
            var text = bounds;
            text.Left += Pixels(14); text.Right -= Pixels(14); text.Top += Pixels(10); text.Bottom = text.Top + Pixels(22);
            Theme.Text(item.DC, row.Title, text, UiFont.Strong, flags: 0x8020);
            text.Top += Pixels(26); text.Bottom = bounds.Bottom - Pixels(7);
            Theme.Text(item.DC, row.Detail, text, UiFont.Caption, WindowTheme.Muted, 0x10);
        }
        return true;
    }
    protected override void Command(int id, int notification)
    {
        if (notification != 0) return;
        if (id == 1 && canApply) { accepted = true; Close(); }
        else if (id == 2) Close();
    }
}
