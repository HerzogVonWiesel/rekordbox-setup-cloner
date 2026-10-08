using System.Runtime.InteropServices;

namespace RekordboxSetupCloner;

// Win32 controls and system dialogs. No managed GUI framework or COM object wrappers.
internal static class Native
{
    internal const uint WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_TABSTOP = 0x10000,
        WS_VSCROLL = 0x200000, WS_OVERLAPPEDWINDOW = 0xCF0000,
        WS_EX_CLIENTEDGE = 0x200, WS_EX_CONTROLPARENT = 0x10000;
    internal const uint WM_CREATE = 1, WM_DESTROY = 2, WM_SIZE = 5, WM_CLOSE = 0x10,
        WM_GETMINMAXINFO = 0x24, WM_SETFONT = 0x30, WM_COMMAND = 0x111, WM_NCCREATE = 0x81,
        WM_NCDESTROY = 0x82, WM_DPICHANGED = 0x2E0, WM_VSCROLL = 0x115, WM_MOUSEWHEEL = 0x20A;
    internal const uint EM_SETLIMITTEXT = 0xC5;
    internal const uint MB_ICONERROR = 0x10;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int BrowseCallback(nint window, uint message, nint lParam, nint data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        internal uint Size, Style;
        internal nint Procedure;
        internal int ClassExtra, WindowExtra;
        internal nint Instance, Icon, Cursor, Background;
        internal string? MenuName;
        internal string ClassName;
        internal nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Id;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal Point Position;
        internal uint Private;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MinMaxInfo { internal Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScrollInfo
    {
        internal uint Size, Mask;
        internal int Minimum, Maximum;
        internal uint Page;
        internal int Position, TrackPosition;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo { internal uint Size; internal Rect Monitor, WorkArea; internal uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct PaintInfo
    {
        internal nint DC;
        internal int Erase;
        internal Rect Bounds;
        internal int Restore, Update;
        internal uint Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7, Reserved8;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct DrawItem
    {
        internal uint Type, ControlId, ItemId, Action, State;
        internal nint Window, DC;
        internal Rect Bounds;
        internal nuint Data;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MeasureItem
    {
        internal uint Type, ControlId, ItemId, Width, Height;
        internal nuint Data;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct OpenFileName
    {
        internal uint Size;
        internal nint Owner, Instance, Filter, CustomFilter;
        internal uint MaxCustomFilter, FilterIndex;
        internal nint File;
        internal uint MaxFile;
        internal nint FileTitle;
        internal uint MaxFileTitle;
        internal nint InitialDirectory, Title;
        internal uint Flags;
        internal ushort FileOffset, FileExtension;
        internal nint DefaultExtension, CustomData, Hook, TemplateName, Reserved;
        internal uint Reserved2, FlagsEx;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BrowseInfo
    {
        internal nint Owner, Root, DisplayName, Title;
        internal uint Flags;
        internal nint Callback, Data;
        internal int Image;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern ushort RegisterClassExW(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint CreateWindowExW(uint extendedStyle, string className, string text, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] internal static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern int GetMessageW(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] internal static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsDialogMessageW(nint window, ref Message message);
    [DllImport("user32.dll")] internal static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnableWindow(nint window, [MarshalAs(UnmanagedType.Bool)] bool enabled);
    [DllImport("user32.dll")] internal static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] internal static extern nint GetFocus();
    [DllImport("user32.dll")] internal static extern nint BeginPaint(nint window, out PaintInfo paint);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EndPaint(nint window, ref PaintInfo paint);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool InvalidateRect(nint window, nint bounds, [MarshalAs(UnmanagedType.Bool)] bool erase);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RedrawWindow(nint window, nint bounds, nint region, uint flags);
    [DllImport("user32.dll")] internal static extern int FillRect(nint dc, ref Rect bounds, nint brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int DrawTextW(nint dc, string text, int length, ref Rect bounds, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DrawFocusRect(nint dc, ref Rect bounds);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DrawIconEx(nint dc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsChild(nint parent, nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ScreenToClient(nint window, ref Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint window, out Rect rectangle);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint BeginDeferWindowPos(int count);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint DeferWindowPos(nint positions, nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EndDeferWindowPos(nint positions);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] internal static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern int SetScrollInfo(nint window, int bar, ref ScrollInfo info, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetScrollInfo(nint window, int bar, ref ScrollInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint SendMessageW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowTextW(nint window, string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowTextLengthW(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowTextW(nint window, [Out] char[] text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int MessageBoxW(nint owner, string text, string caption, uint type);
    [DllImport("user32.dll")] internal static extern nint LoadCursorW(nint instance, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint LoadImageW(nint instance, nint name, uint type, int width, int height, uint flags);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] internal static extern nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeout, uint charset, uint outputPrecision, uint clipPrecision, uint quality, uint pitch, string face);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] internal static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] internal static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] internal static extern int SaveDC(nint dc);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RestoreDC(nint dc, int saved);
    [DllImport("gdi32.dll")] internal static extern int IntersectClipRect(nint dc, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] internal static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll")] internal static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll")] internal static extern uint SetBkColor(nint dc, uint color);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RoundRect(nint dc, int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool MoveToEx(nint dc, int x, int y, nint previous);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LineTo(nint dc, int x, int y);
    [DllImport("ole32.dll")] internal static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] internal static extern void OleUninitialize();
    [DllImport("ole32.dll")] internal static extern void CoTaskMemFree(nint memory);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetOpenFileNameW(ref OpenFileName fileName);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetSaveFileNameW(ref OpenFileName fileName);
    [DllImport("comdlg32.dll")] internal static extern uint CommDlgExtendedError();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern nint SHBrowseForFolderW(ref BrowseInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SHGetPathFromIDListEx(nint item, [Out] char[] path, uint maximum, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern nint ShellExecuteW(nint owner, string operation, string file, string? parameters, string? directory, int show);
}
