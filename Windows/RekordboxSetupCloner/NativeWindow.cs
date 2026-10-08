using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RekordboxSetupCloner;

internal abstract class NativeWindow
{
    private const string ClassName = "RekordboxSetupCloner.NativeWindow";
    private static readonly Native.WindowProcedure Procedure = WindowProcedure;
    private static readonly Dictionary<nint, NativeWindow> Windows = [];
    private static bool registered;
    private GCHandle context;
    private readonly List<nint> controls = [];
    private readonly Dictionary<nint, (UiFont Font, uint Ink, uint Background)> styles = [];
    private readonly Dictionary<nint, (ButtonKind Kind, bool OnSurface)> buttons = [];
    private readonly HashSet<nint> selectedSegments = [];
    protected WindowTheme Theme { get; private set; } = null!;
    private int scrollPosition, scrollMaximum;
    private nint layoutBatch;
    private bool arranging;
    protected bool Closed { get; private set; }
    internal nint Handle { get; private set; }
    protected nint Owner { get; private set; }
    protected abstract string Title { get; }
    protected virtual int InitialWidth => 900;
    protected virtual int InitialHeight => 790;
    protected virtual int MinimumWidth => 860;
    protected virtual int MinimumHeight => 790;
    protected virtual int ContentHeight => 0;
    protected double Scale => (Handle == 0 ? Native.GetDpiForSystem() : Native.GetDpiForWindow(Handle)) / 96.0;
    protected int Pixels(int value) => (int)Math.Round(value * Scale);

    internal void Open(nint owner = 0)
    {
        Register();
        Owner = owner;
        context = GCHandle.Alloc(this);
        var instance = Native.GetModuleHandleW(null);
        var width = Pixels(InitialWidth);
        var height = Pixels(InitialHeight);
        var x = int.MinValue;
        var y = int.MinValue;
        if (owner != 0 && Native.GetWindowRect(owner, out var parent))
        {
            x = parent.Left + (parent.Right - parent.Left - width) / 2;
            y = parent.Top + (parent.Bottom - parent.Top - height) / 2;
        }
        // Composite the parent and child controls together instead of exposing intermediate paints.
        var window = Native.CreateWindowExW(Native.WS_EX_CONTROLPARENT | 0x02000000, ClassName, Title,
            Native.WS_OVERLAPPEDWINDOW | 0x02000000 | (ContentHeight > 0 ? Native.WS_VSCROLL : 0),
            x, y, width, height, owner, 0, instance, GCHandle.ToIntPtr(context));
        if (window == 0)
        {
            if (context.IsAllocated) context.Free();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the application window.");
        }
        var monitor = new Native.MonitorInfo { Size = (uint)Marshal.SizeOf<Native.MonitorInfo>() };
        if (Native.GetMonitorInfoW(Native.MonitorFromWindow(window, 2), ref monitor) && Native.GetWindowRect(window, out var bounds))
        {
            var work = monitor.WorkArea;
            width = Math.Min(bounds.Right - bounds.Left, work.Right - work.Left);
            height = Math.Min(bounds.Bottom - bounds.Top, work.Bottom - work.Top);
            x = Math.Clamp(bounds.Left, work.Left, work.Right - width);
            y = Math.Clamp(bounds.Top, work.Top, work.Bottom - height);
            Native.SetWindowPos(window, 0, x, y, width, height, 0x14);
        }
        Native.ShowWindow(window, 5);
        Native.UpdateWindow(window);
    }

    internal void RunMessageLoop()
    {
        while (!Closed)
        {
            var result = Native.GetMessageW(out var message, 0, 0, 0);
            if (result < 0) throw new Win32Exception();
            if (result == 0)
            {
                if (Owner != 0) Native.PostQuitMessage((int)message.WParam);
                break;
            }
            if (!Native.IsDialogMessageW(Handle, ref message))
            {
                Native.TranslateMessage(ref message);
                Native.DispatchMessageW(ref message);
            }
            else if (!Closed && message.Id == 0x100 && message.WParam == 9) RevealFocusedControl(); // Tab navigation only.
        }
    }

    protected nint Control(string className, string text, int id, uint style = 0, uint extendedStyle = 0)
    {
        var control = Native.CreateWindowExW(extendedStyle, className, text, Native.WS_CHILD | Native.WS_VISIBLE | style,
            0, 0, 1, 1, Handle, id, Native.GetModuleHandleW(null), 0);
        if (control == 0) throw new Win32Exception();
        controls.Add(control);
        Native.SendMessageW(control, Native.WM_SETFONT, (nuint)Theme.Font(UiFont.Body), 1);
        return control;
    }
    protected nint Label(string text, int id = 0) => Control("STATIC", text, id, 0x80); // SS_NOPREFIX
    protected nint Heading(string text)
    {
        var control = Label(text);
        Style(control, UiFont.Hero);
        return control;
    }
    protected nint StyledLabel(string text, UiFont font = UiFont.Body, uint ink = WindowTheme.Ink, bool onSurface = false)
    {
        var control = Label(text);
        Style(control, font, ink, onSurface);
        return control;
    }
    protected void Style(nint control, UiFont font = UiFont.Body, uint ink = WindowTheme.Ink, bool onSurface = false)
    {
        styles[control] = (font, ink, onSurface ? WindowTheme.Surface : WindowTheme.Background);
        Native.SendMessageW(control, Native.WM_SETFONT, (nuint)Theme.Font(font), 1);
    }
    protected void Background(nint control, uint color)
    {
        var current = styles.TryGetValue(control, out var style) ? style : (UiFont.Body, WindowTheme.Ink, WindowTheme.Background);
        styles[control] = (current.Item1, current.Item2, color);
    }
    protected nint Button(string text, int id, ButtonKind kind = ButtonKind.Secondary, bool onSurface = true)
    {
        var control = Control("BUTTON", text.Replace("&", "&&"), id, Native.WS_TABSTOP | 0xB);
        buttons[control] = (kind, onSurface);
        return control;
    }
    protected void SelectSegment(nint control, bool selected)
    {
        if (selected) selectedSegments.Add(control); else selectedSegments.Remove(control);
        var caption = Text(control).Replace(" (selected)", "");
        SetText(control, caption + (selected ? " (selected)" : ""));
        Native.InvalidateRect(control, 0, false);
    }
    protected nint Edit(string text, int id)
    {
        var edit = Control("EDIT", text, id, Native.WS_TABSTOP | 0x80, Native.WS_EX_CLIENTEDGE);
        Native.SendMessageW(edit, Native.EM_SETLIMITTEXT, 120, 0);
        Native.SendMessageW(edit, 0xD3, 3, (nint)(Pixels(8) | (Pixels(8) << 16)));
        return edit;
    }
    protected void Place(nint control, int x, int y, int width, int height)
    {
        // No copied pixels: a control moving into view may have been partially clipped before.
        const uint flags = 0x4 | 0x8 | 0x10 | 0x100; // NOZORDER | NOREDRAW | NOACTIVATE | NOCOPYBITS
        if (layoutBatch != 0)
        {
            layoutBatch = Native.DeferWindowPos(layoutBatch, control, 0, Pixels(x), Pixels(y) - scrollPosition,
                Pixels(Math.Max(1, width)), Pixels(Math.Max(1, height)), flags);
            if (layoutBatch == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update the window layout.");
        }
        else Native.SetWindowPos(control, 0, Pixels(x), Pixels(y) - scrollPosition,
            Pixels(Math.Max(1, width)), Pixels(Math.Max(1, height)), flags);
    }
    protected void RefreshLayout() => Resize();
    protected static string Text(nint control)
    {
        var buffer = new char[Native.GetWindowTextLengthW(control) + 1];
        var length = Native.GetWindowTextW(control, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }
    protected static void SetText(nint control, string text) => Native.SetWindowTextW(control, text);
    protected void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { Native.MessageBoxW(Handle, error.Message, "Couldn’t complete the operation", Native.MB_ICONERROR); }
    }
    protected void Close() { if (!Closed) Native.DestroyWindow(Handle); }
    protected abstract void CreateControls();
    protected abstract void Layout(int width, int height);
    protected abstract void Command(int id, int notification);
    protected virtual void Paint(nint dc, int width, int height) { }
    protected virtual bool DrawItem(Native.DrawItem item) => false;
    protected Native.Rect Bounds(int x, int y, int width, int height) => new()
    {
        Left = Pixels(x), Top = Pixels(y) - scrollPosition,
        Right = Pixels(x + width), Bottom = Pixels(y + height) - scrollPosition
    };

    private void Fonts()
    {
        var old = Theme;
        Theme = new WindowTheme(Scale);
        foreach (var control in controls)
            Native.SendMessageW(control, Native.WM_SETFONT, (nuint)Theme.Font(styles.TryGetValue(control, out var style) ? style.Font : UiFont.Body), 1);
        old?.Dispose();
    }
    private nint Message(uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case Native.WM_CREATE:
                Fonts();
                CreateControls();
                var instance = Native.GetModuleHandleW(null);
                var icon = Native.LoadImageW(instance, 32512, 1, Pixels(32), Pixels(32), 0x8000);
                if (icon != 0) Native.SendMessageW(Handle, 0x80, 1, icon);
                Resize();
                return 0;
            case Native.WM_SIZE:
                Resize();
                return 0;
            case 0xF: // WM_PAINT
                var dc = Native.BeginPaint(Handle, out var paint);
                try
                {
                    Native.GetClientRect(Handle, out var client);
                    Theme.Fill(dc, client, WindowTheme.Background);
                    Paint(dc, (int)Math.Round(client.Right / Scale), Math.Max(ContentHeight, (int)Math.Round(client.Bottom / Scale)));
                }
                finally { Native.EndPaint(Handle, ref paint); }
                return 0;
            case 0x14: return 1; // WM_ERASEBKGND: paint the background in one pass.
            case 0x138: // WM_CTLCOLORSTATIC
                var labelStyle = styles.TryGetValue(lParam, out var style) ? style : (UiFont.Body, WindowTheme.Ink, WindowTheme.Background);
                Native.SetTextColor((nint)wParam, WindowTheme.Color(labelStyle.Item2));
                Native.SetBkColor((nint)wParam, WindowTheme.Color(labelStyle.Item3));
                return Theme.Brush(labelStyle.Item3);
            case 0x133: // WM_CTLCOLOREDIT
                Native.SetTextColor((nint)wParam, WindowTheme.Color(WindowTheme.Ink));
                Native.SetBkColor((nint)wParam, WindowTheme.Color(WindowTheme.Surface));
                return Theme.Brush(WindowTheme.Surface);
            case 0x134: // WM_CTLCOLORLISTBOX
                Native.SetTextColor((nint)wParam, WindowTheme.Color(WindowTheme.Ink));
                Native.SetBkColor((nint)wParam, WindowTheme.Color(WindowTheme.Background));
                return Theme.Brush(WindowTheme.Background);
            case 0x2B: // WM_DRAWITEM
                var item = Marshal.PtrToStructure<Native.DrawItem>(lParam);
                var saved = Native.SaveDC(item.DC);
                try
                {
                    Native.IntersectClipRect(item.DC, item.Bounds.Left, item.Bounds.Top, item.Bounds.Right, item.Bounds.Bottom);
                    if (buttons.TryGetValue(item.Window, out var button))
                    {
                        Theme.Button(item, button.Kind, selectedSegments.Contains(item.Window), button.OnSurface);
                        return 1;
                    }
                    return DrawItem(item) ? 1 : 0;
                }
                finally { if (saved != 0) Native.RestoreDC(item.DC, saved); }
            case 0x2C: // WM_MEASUREITEM: individual list rows can refine this after insertion.
                var measure = Marshal.PtrToStructure<Native.MeasureItem>(lParam);
                measure.Height = (uint)Pixels(78);
                Marshal.StructureToPtr(measure, lParam, false);
                return 1;
            case Native.WM_VSCROLL when ContentHeight > 0:
                Scroll((int)(wParam & 0xFFFF));
                return 0;
            case Native.WM_MOUSEWHEEL when ContentHeight > 0:
                scrollPosition -= (short)((wParam >> 16) & 0xFFFF) * Pixels(60) / 120;
                Resize();
                return 0;
            case Native.WM_COMMAND:
                Command((int)(wParam & 0xFFFF), (int)((wParam >> 16) & 0xFFFF));
                return 0;
            case Native.WM_GETMINMAXINFO:
                var minimum = Marshal.PtrToStructure<Native.MinMaxInfo>(lParam);
                minimum.MinTrackSize = new Native.Point { X = Pixels(MinimumWidth), Y = Pixels(MinimumHeight) };
                Marshal.StructureToPtr(minimum, lParam, false);
                return 0;
            case Native.WM_DPICHANGED:
                Fonts();
                var suggested = Marshal.PtrToStructure<Native.Rect>(lParam);
                Native.SetWindowPos(Handle, 0, suggested.Left, suggested.Top, suggested.Right - suggested.Left, suggested.Bottom - suggested.Top, 0x14);
                Resize();
                return 0;
            case Native.WM_CLOSE:
                Close();
                return 0;
            case Native.WM_DESTROY:
                Closed = true;
                if (Owner == 0) Native.PostQuitMessage(0);
                return 0;
            case Native.WM_NCDESTROY:
                Windows.Remove(Handle);
                if (context.IsAllocated) context.Free();
                Theme?.Dispose();
                return Native.DefWindowProcW(Handle, message, wParam, lParam);
        }
        return Native.DefWindowProcW(Handle, message, wParam, lParam);
    }
    private void Resize()
    {
        if (arranging || Theme == null || !Native.GetClientRect(Handle, out var rectangle) || rectangle.Bottom <= 0) return;
        arranging = true;
        try
        {
            var height = rectangle.Bottom - rectangle.Top;
            if (ContentHeight > 0)
            {
                scrollMaximum = Math.Max(0, Pixels(ContentHeight) - height);
                scrollPosition = Math.Clamp(scrollPosition, 0, scrollMaximum);
                var info = new Native.ScrollInfo
                {
                    Size = (uint)Marshal.SizeOf<Native.ScrollInfo>(), Mask = 0xF,
                    Maximum = Math.Max(height, Pixels(ContentHeight)) - 1, Page = (uint)height, Position = scrollPosition
                };
                Native.SetScrollInfo(Handle, 1, ref info, true);
            }
            layoutBatch = Native.BeginDeferWindowPos(controls.Count);
            if (layoutBatch == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update the window layout.");
            try
            {
                Layout((int)Math.Round((rectangle.Right - rectangle.Left) / Scale),
                    Math.Max(ContentHeight, (int)Math.Round(height / Scale)));
            }
            finally
            {
                var batch = layoutBatch;
                layoutBatch = 0;
                if (batch != 0 && !Native.EndDeferWindowPos(batch))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update the window layout.");
            }
            // Repaint every child, including parts that were offscreen before this layout change.
            Native.RedrawWindow(Handle, 0, 0, 0x1 | 0x4 | 0x80 | 0x100 | 0x400);
        }
        finally { arranging = false; }
    }
    private void Scroll(int command)
    {
        Native.GetClientRect(Handle, out var bounds);
        var page = Math.Max(Pixels(30), bounds.Bottom - bounds.Top - Pixels(30));
        var info = new Native.ScrollInfo { Size = (uint)Marshal.SizeOf<Native.ScrollInfo>(), Mask = 0x10 };
        switch (command)
        {
            case 0: scrollPosition -= Pixels(30); break;
            case 1: scrollPosition += Pixels(30); break;
            case 2: scrollPosition -= page; break;
            case 3: scrollPosition += page; break;
            case 4:
            case 5:
                if (Native.GetScrollInfo(Handle, 1, ref info)) scrollPosition = info.TrackPosition;
                break;
            case 6: scrollPosition = 0; break;
            case 7: scrollPosition = scrollMaximum; break;
        }
        Resize();
    }
    private void RevealFocusedControl()
    {
        if (ContentHeight == 0) return;
        var focus = Native.GetFocus();
        if (!Native.IsChild(Handle, focus) || !Native.GetWindowRect(focus, out var bounds)) return;
        var top = new Native.Point { X = bounds.Left, Y = bounds.Top };
        var bottom = new Native.Point { X = bounds.Right, Y = bounds.Bottom };
        Native.ScreenToClient(Handle, ref top);
        Native.ScreenToClient(Handle, ref bottom);
        Native.GetClientRect(Handle, out var client);
        if (top.Y < 0) scrollPosition += top.Y;
        else if (bottom.Y > client.Bottom) scrollPosition += bottom.Y - client.Bottom;
        else return;
        Resize();
    }
    private static nint WindowProcedure(nint handle, uint message, nuint wParam, nint lParam)
    {
        if (message == Native.WM_NCCREATE)
        {
            var window = (NativeWindow)GCHandle.FromIntPtr(Marshal.ReadIntPtr(lParam)).Target!;
            window.Handle = handle;
            Windows[handle] = window;
        }
        if (!Windows.TryGetValue(handle, out var current)) return Native.DefWindowProcW(handle, message, wParam, lParam);
        try { return current.Message(message, wParam, lParam); }
        catch (Exception error)
        {
            Native.MessageBoxW(handle, error.Message, "Rekordbox Setup Cloner", Native.MB_ICONERROR);
            return message == Native.WM_CREATE ? -1 : Native.DefWindowProcW(handle, message, wParam, lParam);
        }
    }
    private static void Register()
    {
        if (registered) return;
        var definition = new Native.WindowClass
        {
            Size = (uint)Marshal.SizeOf<Native.WindowClass>(), ClassName = ClassName,
            Instance = Native.GetModuleHandleW(null), Procedure = Marshal.GetFunctionPointerForDelegate(Procedure),
            Cursor = Native.LoadCursorW(0, 32512), Background = 6 // COLOR_WINDOW + 1
        };
        if (Native.RegisterClassExW(ref definition) == 0) throw new Win32Exception();
        registered = true;
    }
}
