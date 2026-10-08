using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RekordboxSetupCloner;

internal static class NativeDialogs
{
    private static readonly Native.BrowseCallback BrowseCallback = InitializeFolder;

    internal static string? OpenFile(nint owner, string title, string filter, string? initialDirectory = null) =>
        File(owner, title, filter, initialDirectory, null, null, false);
    internal static string? SaveFile(nint owner, string title, string filter, string fileName, string extension) =>
        File(owner, title, filter, null, fileName, extension, true);

    private static string? File(nint owner, string title, string filter, string? directory, string? name, string? extension, bool save)
    {
        using var strings = new NativeStrings();
        var buffer = strings.Buffer(32768);
        if (name != null) Marshal.Copy((name + '\0').ToCharArray(), 0, buffer, name.Length + 1);
        var info = new Native.OpenFileName
        {
            Size = (uint)Marshal.SizeOf<Native.OpenFileName>(), Owner = owner,
            Filter = strings.Add(filter), FilterIndex = 1, File = buffer, MaxFile = 32768,
            Title = strings.Add(title), InitialDirectory = strings.Add(directory), DefaultExtension = strings.Add(extension),
            Flags = 0x80000 | 0x8 | 0x800 | 0x4 | (save ? 0x2u : 0x1000u)
        };
        if (save ? Native.GetSaveFileNameW(ref info) : Native.GetOpenFileNameW(ref info)) return Marshal.PtrToStringUni(buffer);
        var error = Native.CommDlgExtendedError();
        if (error != 0) throw new Win32Exception((int)error, $"The file dialog failed (0x{error:x}).");
        return null;
    }

    internal static string? Folder(nint owner, string initialDirectory)
    {
        using var strings = new NativeStrings();
        var info = new Native.BrowseInfo
        {
            Owner = owner, DisplayName = strings.Buffer(260), Title = strings.Add("Choose the rekordbox settings folder"),
            Flags = 0x1 | 0x40 | 0x10, Callback = Marshal.GetFunctionPointerForDelegate(BrowseCallback), Data = strings.Add(initialDirectory)
        };
        var item = Native.SHBrowseForFolderW(ref info);
        if (item == 0) return null;
        try
        {
            var path = new char[32768];
            if (!Native.SHGetPathFromIDListEx(item, path, (uint)path.Length, 0)) throw new SetupException("Choose a filesystem folder.");
            var end = Array.IndexOf(path, '\0');
            return new string(path, 0, end < 0 ? path.Length : end);
        }
        finally { Native.CoTaskMemFree(item); }
    }
    private static int InitializeFolder(nint window, uint message, nint lParam, nint data)
    {
        if (message == 1 && data != 0) Native.SendMessageW(window, 0x467, 1, data);
        return 0;
    }

    private sealed class NativeStrings : IDisposable
    {
        private readonly List<nint> allocations = [];
        internal nint Add(string? value)
        {
            if (value == null) return 0;
            var memory = Marshal.StringToHGlobalUni(value);
            allocations.Add(memory);
            return memory;
        }
        internal nint Buffer(int characters)
        {
            var memory = Marshal.AllocHGlobal(characters * sizeof(char));
            Marshal.Copy(new char[characters], 0, memory, characters);
            allocations.Add(memory);
            return memory;
        }
        public void Dispose() { foreach (var memory in allocations) Marshal.FreeHGlobal(memory); }
    }
}
