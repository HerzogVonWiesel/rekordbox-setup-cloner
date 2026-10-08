namespace RekordboxSetupCloner;

internal enum UiFont { Body, Caption, Section, Hero, Strong, Mono, Icon }
internal enum ButtonKind { Secondary, Primary, Link, Segment }

// Styling uses the Windows graphics API; the executable stays native and self-contained.
internal sealed class WindowTheme(double scale) : IDisposable
{
    internal const uint Background = 0xF8F9FC, Surface = 0xFFFFFF, Ink = 0x202938,
        Muted = 0x687386, Accent = 0x2463EB, AccentSoft = 0xEEF4FF, Border = 0xDFE5EE,
        Disabled = 0xA0A8B5, Warning = 0x94601D, WarningSoft = 0xFFF7E8;
    private readonly Dictionary<uint, nint> brushes = [];
    private readonly Dictionary<UiFont, nint> fonts = [];
    internal int Pixels(int value) => (int)Math.Round(value * scale);
    internal static uint Color(uint rgb) => (rgb >> 16) | (rgb & 0xFF00) | ((rgb & 0xFF) << 16);
    internal nint Brush(uint color)
    {
        if (!brushes.TryGetValue(color, out var brush)) brushes[color] = brush = Native.CreateSolidBrush(Color(color));
        return brush;
    }
    internal nint Font(UiFont style)
    {
        if (fonts.TryGetValue(style, out var font)) return font;
        var (size, weight, face) = style switch
        {
            UiFont.Caption => (12, 400, "Segoe UI"),
            UiFont.Section => (17, 700, "Segoe UI"),
            UiFont.Hero => (30, 700, "Segoe UI"),
            UiFont.Strong => (14, 700, "Segoe UI"),
            UiFont.Mono => (12, 400, "Consolas"),
            UiFont.Icon => (19, 400, "Segoe MDL2 Assets"),
            _ => (14, 400, "Segoe UI")
        };
        fonts[style] = font = Native.CreateFontW(-Pixels(size), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, 5, 0, face);
        return font;
    }
    internal void Fill(nint dc, Native.Rect bounds, uint color) => Native.FillRect(dc, ref bounds, Brush(color));
    internal void Box(nint dc, Native.Rect bounds, uint fill, uint border, int radius = 10)
    {
        var pen = Native.CreatePen(0, Math.Max(1, Pixels(1)), Color(border));
        var oldPen = Native.SelectObject(dc, pen);
        var oldBrush = Native.SelectObject(dc, Brush(fill));
        Native.RoundRect(dc, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, Pixels(radius * 2), Pixels(radius * 2));
        Native.SelectObject(dc, oldBrush);
        Native.SelectObject(dc, oldPen);
        Native.DeleteObject(pen);
    }
    internal void Text(nint dc, string text, Native.Rect bounds, UiFont font = UiFont.Body,
        uint color = Ink, uint flags = 0)
    {
        var old = Native.SelectObject(dc, Font(font));
        Native.SetBkMode(dc, 1);
        Native.SetTextColor(dc, Color(color));
        Native.DrawTextW(dc, text, text.Length, ref bounds, 0x800 | flags);
        Native.SelectObject(dc, old);
    }
    internal void Line(nint dc, int x1, int y1, int x2, int y2, uint color = Border, int width = 1)
    {
        var pen = Native.CreatePen(0, Math.Max(1, Pixels(width)), Color(color));
        var old = Native.SelectObject(dc, pen);
        Native.MoveToEx(dc, x1, y1, 0);
        Native.LineTo(dc, x2, y2);
        Native.SelectObject(dc, old);
        Native.DeleteObject(pen);
    }
    internal void Button(Native.DrawItem item, ButtonKind kind, bool selected = false, bool onSurface = true)
    {
        var bounds = item.Bounds;
        var disabled = (item.State & 4) != 0;
        var pressed = (item.State & 1) != 0;
        var fill = kind switch
        {
            ButtonKind.Primary => disabled ? Border : pressed ? 0x194EC2u : Accent,
            ButtonKind.Link => Background,
            ButtonKind.Segment => selected ? Surface : 0xEDF0F5u,
            _ => pressed ? AccentSoft : Surface
        };
        var ink = disabled ? Disabled : kind == ButtonKind.Primary ? Surface :
            kind == ButtonKind.Link || kind == ButtonKind.Segment && selected ? Accent : Ink;
        // Clear the corners to match the surrounding card or page.
        Fill(item.DC, bounds, kind == ButtonKind.Link || !onSurface ? Background : Surface);
        bounds.Left += Pixels(1); bounds.Top += Pixels(1); bounds.Right -= Pixels(1); bounds.Bottom -= Pixels(1);
        if (kind != ButtonKind.Link) Box(item.DC, bounds, fill, kind == ButtonKind.Primary ? fill : Border, 7);
        Text(item.DC, GetText(item.Window), bounds, UiFont.Strong, ink, 0x25);
        if ((item.State & 0x10) != 0 && (item.State & 0x200) == 0)
        {
            bounds.Left += Pixels(5); bounds.Right -= Pixels(5); bounds.Top += Pixels(5); bounds.Bottom -= Pixels(5);
            Native.DrawFocusRect(item.DC, ref bounds);
        }
    }
    private static string GetText(nint window)
    {
        var text = new char[Native.GetWindowTextLengthW(window) + 1];
        var length = Native.GetWindowTextW(window, text, text.Length);
        return new string(text, 0, length).Replace("&&", "&").Replace(" (selected)", "");
    }
    public void Dispose()
    {
        foreach (var brush in brushes.Values) Native.DeleteObject(brush);
        foreach (var font in fonts.Values) Native.DeleteObject(font);
    }
}
