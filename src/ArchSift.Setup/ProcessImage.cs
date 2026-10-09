using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArchSift.Setup;

internal static class ProcessImage
{
    public static string? Read(Process process)
    {
        if (!OperatingSystem.IsWindows()) return process.MainModule?.FileName;
        // Module enumeration requires VM_READ; image identity needs only QUERY_LIMITED_INFORMATION.
        using var handle = OpenProcess(0x1000, false, process.Id);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer = new StringBuilder(32768); var length = buffer.Capacity;
        if (!QueryFullProcessImageName(handle, 0, buffer, ref length)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var path = buffer.ToString();
        return path.StartsWith("\\\\?\\", StringComparison.Ordinal) ? path[4..] : path;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref int size);
}
