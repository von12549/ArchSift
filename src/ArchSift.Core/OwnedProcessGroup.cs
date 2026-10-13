using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArchSift.Core;

/// <summary>Windows owned job; never enumerates or terminates processes by name.</summary>
internal sealed class OwnedProcessGroup : IDisposable
{
    private readonly SafeFileHandle handle;
    public OwnedProcessGroup(Process process)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Evidence build jobs require Windows owned process groups.");
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid) throw new IOException("Unable to create owned process group.");
        var limits = new ExtendedLimit { Basic = new() { LimitFlags = 0x2000 } };
        if (!SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimit>()) || !AssignProcessToJobObject(handle, process.Handle))
        { handle.Dispose(); throw new IOException("Unable to assign evidence process ownership."); }
    }
    public bool StopAndWait()
    {
        var wait = Stopwatch.StartNew();
        while (wait.ElapsedMilliseconds < 250 && Active() != 0) Thread.Sleep(10);
        if (Active() != 0 && !TerminateJobObject(handle, 130)) return false;
        while (wait.Elapsed < TimeSpan.FromSeconds(10)) { if (Active() == 0) return true; Thread.Sleep(20); }
        return false;
    }
    private uint Active()
    {
        if (!QueryInformationJobObject(handle, 1, out var information, (uint)Marshal.SizeOf<Accounting>(), IntPtr.Zero))
            throw new IOException("Unable to verify owned process group exit.");
        return information.ActiveProcesses;
    }
    public void Dispose() => handle.Dispose();
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimit
    { public long ProcessTime, JobTime; public uint LimitFlags; public UIntPtr MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperation, WriteOperation, OtherOperation, ReadTransfer, WriteTransfer, OtherTransfer; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimit
    { public BasicLimit Basic; public IoCounters Io; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemory, PeakJobMemory; }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting
    { public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime; public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int type, ref ExtendedLimit info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateJobObject(SafeFileHandle job, uint code);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryInformationJobObject(SafeFileHandle job, int type, out Accounting info, uint length, IntPtr returnedLength);
}
