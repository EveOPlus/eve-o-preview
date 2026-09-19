using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using EveOPreview.Services.Implementation;

namespace EveOPreview.Services.Interop;

internal sealed record CpuSetConstraints(uint[] OriginalSets, ushort[] Groups, ulong HardMask)
{
    internal bool Allows(CpuSet cpu) => Groups.Contains(cpu.Group)
        && (OriginalSets.Length == 0 || OriginalSets.Contains(cpu.Id))
        && (Groups.Length != 1 || HardMask == 0 || (HardMask & (1UL << cpu.LogicalProcessor)) != 0);
}

internal interface ICpuSetApi
{
    CpuSet[] ReadTopology();
    bool IsProcess(IntPtr handle, int processId);
    bool TryReadConstraints(IntPtr handle, out CpuSetConstraints constraints);
    bool TrySet(IntPtr handle, uint[] ids);
}

internal sealed unsafe partial class WindowsCpuSetApi : ICpuSetApi
{
    private const int InsufficientBuffer = 122;

    public CpuSet[] ReadTopology()
    {
        uint required = 0;
        GetSystemCpuSetInformation(null, 0, out required, IntPtr.Zero, 0);
        for (int attempt = 0; attempt < 3 && required > 0 && required <= 16 * 1024 * 1024; attempt++)
        {
            byte[] buffer = new byte[required];
            fixed (byte* pointer = buffer)
            {
                if (GetSystemCpuSetInformation(pointer, (uint)buffer.Length, out required, IntPtr.Zero, 0))
                    return ParseTopology(buffer.AsSpan(0, checked((int)required)));
            }
            if (Marshal.GetLastPInvokeError() != InsufficientBuffer) break;
        }
        throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows CPU-set topology is unavailable");
    }

    internal static CpuSet[] ParseTopology(ReadOnlySpan<byte> buffer)
    {
        var result = new List<CpuSet>();
        while (!buffer.IsEmpty)
        {
            if (buffer.Length < 8) throw new InvalidOperationException("Truncated CPU-set topology");
            uint size = BitConverter.ToUInt32(buffer);
            uint type = BitConverter.ToUInt32(buffer[4..]);
            if (size < 8 || size > buffer.Length) throw new InvalidOperationException("Invalid CPU-set record size");
            if (type == 0)
            {
                if (size < 32 || buffer[14] >= 64) throw new InvalidOperationException("Invalid CPU-set record");
                // Do not assign CPU sets exclusively allocated to another process.
                // Parked CPUs remain eligible: Windows owns parking and power policy.
                if ((buffer[19] & 2) == 0)
                    result.Add(new(BitConverter.ToUInt32(buffer[8..]), BitConverter.ToUInt16(buffer[12..]),
                        buffer[14], buffer[15], buffer[16], buffer[17], buffer[18]));
            }
            buffer = buffer[(int)size..];
        }
        if (result.Select(c => c.Id).Distinct().Count() != result.Count ||
            result.Select(c => (c.Group, c.LogicalProcessor)).Distinct().Count() != result.Count)
            throw new InvalidOperationException("Duplicate CPU-set topology entries");
        return result.OrderBy(c => c.Id).ToArray();
    }

    public bool IsProcess(IntPtr handle, int processId) => KernelNativeMethods.GetProcessId(handle) == processId;

    public bool TryReadConstraints(IntPtr handle, out CpuSetConstraints constraints)
    {
        constraints = null;
        if (!TryReadDefaultSets(handle, out var original)) return false;
        // Windows supports at most 64 groups. Request all slots in one call.
        ushort[] groups = new ushort[64];
        ushort count = (ushort)groups.Length;
        fixed (ushort* pointer = groups)
            if (!GetProcessGroupAffinity(handle, ref count, pointer) || count == 0 || count > groups.Length) return false;
        if (!KernelNativeMethods.GetProcessAffinityMask(handle, out var processMask, out _)) return false;
        constraints = new(original, groups.Take(count).ToArray(), unchecked((ulong)processMask.ToInt64()));
        return true;
    }

    internal static bool TryReadDefaultSets(IntPtr handle, out uint[] ids)
    {
        ids = [];
        uint required = 0;
        if (GetProcessDefaultCpuSets(handle, null, 0, out required) && required == 0) return true;
        for (int attempt = 0; attempt < 3 && required > 0 && required <= 65536; attempt++)
        {
            var buffer = new uint[required];
            fixed (uint* pointer = buffer)
            {
                if (GetProcessDefaultCpuSets(handle, pointer, (uint)buffer.Length, out required))
                {
                    ids = buffer.Take(checked((int)required)).Order().ToArray();
                    return true;
                }
            }
            if (Marshal.GetLastPInvokeError() != InsufficientBuffer) break;
        }
        return false;
    }

    public bool TrySet(IntPtr handle, uint[] ids)
    {
        fixed (uint* pointer = ids)
            return SetProcessDefaultCpuSets(handle, pointer, (uint)ids.Length);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemCpuSetInformation(byte* information, uint bufferLength, out uint returnedLength, IntPtr process, uint flags);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessDefaultCpuSets(IntPtr process, uint* ids, uint count, out uint required);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDefaultCpuSets(IntPtr process, uint* ids, uint count);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessGroupAffinity(IntPtr process, ref ushort count, ushort* groups);
}
