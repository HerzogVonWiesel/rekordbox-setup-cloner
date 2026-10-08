namespace RekordboxSetupCloner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Native.SetProcessDpiAwarenessContext(new nint(-4));
            var result = Native.OleInitialize(0);
            if (result < 0) System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(result);
            try
            {
                var window = new MainWindow();
                window.Open();
                window.RunMessageLoop();
            }
            finally { Native.OleUninitialize(); }
        }
        catch (Exception error)
        {
            Native.MessageBoxW(0, error.Message, "Rekordbox Setup Cloner", Native.MB_ICONERROR);
        }
    }
}
