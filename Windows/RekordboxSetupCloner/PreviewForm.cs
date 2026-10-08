namespace RekordboxSetupCloner;

internal sealed class PreviewForm : Form
{
    public PreviewForm(string title, string description, List<Change> changes, List<string> skipped, string note, string applyTitle, bool canApply)
    {
        Text = title;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 620);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(content);
        content.Controls.Add(new Label { Text = description, UseMnemonic = false, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 14) }, 0, 0);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = SystemColors.Window, AccessibleName = "Preview changes"
        };
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.Columns.Add("group", "Group");
        grid.Columns.Add("setting", "Preference / file");
        grid.Columns.Add("change", "Change");
        grid.Columns[0].FillWeight = 22;
        grid.Columns[1].FillWeight = 35;
        grid.Columns[2].FillWeight = 43;
        foreach (var change in changes) grid.Rows.Add(change.Group.Title(), change.Title, change.Detail);
        if (changes.Count == 0) grid.Rows.Add("", "No changes to apply", "The selected settings already match, or none are compatible.");
        content.Controls.Add(grid, 0, 1);
        if (skipped.Count > 0)
        {
            var skipPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1 };
            var toggle = new CheckBox { Text = $"Show skipped items ({skipped.Count})", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            var reasons = new TextBox { Text = string.Join(Environment.NewLine, skipped), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Top, Height = 110, Visible = false, AccessibleName = "Skipped items" };
            toggle.CheckedChanged += (_, _) => reasons.Visible = toggle.Checked;
            skipPanel.Controls.Add(toggle);
            skipPanel.Controls.Add(reasons);
            content.Controls.Add(skipPanel, 0, 2);
        }
        content.Controls.Add(new Label { Text = note, UseMnemonic = false, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 14, 3, 12) }, 0, 3);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft };
        var apply = new Button { Text = applyTitle, UseMnemonic = false, AutoSize = true, DialogResult = DialogResult.OK, Enabled = canApply, Padding = new Padding(8, 4, 8, 4) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 4, 8, 4) };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        content.Controls.Add(buttons, 0, 4);
        AcceptButton = apply;
        CancelButton = cancel;
    }
}
