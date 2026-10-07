using System;
using System.Buffers.Text;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace EveOPreview.Services.Implementation;

/// <summary>Reads only the user identifier from a discovered client's launch token. Never authenticates requests.</summary>
public sealed class EveClientUserIdReader
{
    public long? Read(int processId, string expectedTitle)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.ProcessName.Equals("ExeFile", StringComparison.OrdinalIgnoreCase)
                || process.MainWindowTitle != expectedTitle) return null;
            using var handle = OpenProcess(0x1000, false, processId); // Query limited information only.
            if (handle.IsInvalid) return null;
            var userId = ReadSubject(handle);
            process.Refresh();
            return !process.HasExited && process.MainWindowTitle == expectedTitle ? userId : null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or EntryPointNotFoundException)
        {
            // Command lines and native exception details must never reach logs or UI feedback.
            return null;
        }
    }

    private static unsafe long? ReadSubject(SafeProcessHandle handle)
    {
        const int commandLineInformation = 60;
        NtQueryInformationProcess(handle, commandLineInformation, IntPtr.Zero, 0, out int length);
        if (length < Marshal.SizeOf<UnicodeString>() || length > 131072) return null;
        var memory = Marshal.AllocHGlobal(length);
        try
        {
            if (NtQueryInformationProcess(handle, commandLineInformation, memory, length, out _) < 0) return null;
            var value = Marshal.PtrToStructure<UnicodeString>(memory);
            long offset = value.Buffer.ToInt64() - memory.ToInt64();
            if (value.Length % 2 != 0 || offset < Marshal.SizeOf<UnicodeString>() || offset + value.Length > length) return null;
            return ExtractUserId(new ReadOnlySpan<char>((void*)value.Buffer, value.Length / 2));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(new Span<byte>((void*)memory, length));
            Marshal.FreeHGlobal(memory);
        }
    }

    public static long? ExtractUserId(ReadOnlySpan<char> commandLine)
    {
        if (commandLine.Length > 65536) return null;
        // Accept launcher /ssoToken=VALUE and --ssoToken VALUE forms, including quoted values.
        for (int start = 0; start < commandLine.Length; start++)
        {
            if (start > 0 && !char.IsWhiteSpace(commandLine[start - 1]) && commandLine[start - 1] != '"') continue;
            var remaining = commandLine[start..];
            int prefix = remaining.StartsWith("/ssoToken", StringComparison.OrdinalIgnoreCase) ? 9
                : remaining.StartsWith("--ssoToken", StringComparison.OrdinalIgnoreCase) ? 10 : 0;
            if (prefix == 0 || remaining.Length <= prefix) continue;
            if (remaining[prefix] != '=' && !char.IsWhiteSpace(remaining[prefix])) continue;
            var token = remaining[(prefix + 1)..].TrimStart();
            if (!token.IsEmpty && token[0] == '"') token = token[1..];
            int end = 0;
            while (end < token.Length && token[end] != '"' && !char.IsWhiteSpace(token[end])) end++;
            return DecodeSubject(token[..end]);
        }
        return null;
    }

    private static long? DecodeSubject(ReadOnlySpan<char> token)
    {
        int first = token.IndexOf('.');
        if (first <= 0) return null;
        int second = token[(first + 1)..].IndexOf('.');
        if (second <= 0 || first + second + 2 >= token.Length) return null;
        var payload = token.Slice(first + 1, second);
        if (payload.Length > 32768 || payload.Length % 4 == 1) return null;
        var encoded = new char[(payload.Length + 3) / 4 * 4];
        var decoded = new byte[encoded.Length / 4 * 3];
        try
        {
            payload.CopyTo(encoded);
            for (int i = 0; i < payload.Length; i++) encoded[i] = encoded[i] switch { '-' => '+', '_' => '/', var c => c };
            encoded.AsSpan(payload.Length).Fill('=');
            if (!Convert.TryFromBase64Chars(encoded, decoded, out int written)) return null;
            var reader = new Utf8JsonReader(decoded.AsSpan(0, written), new JsonReaderOptions { MaxDepth = 16 });
            long? result = null;
            bool found = false;
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1 || !reader.ValueTextEquals("sub")) continue;
                if (found || !reader.Read() || reader.TokenType != JsonTokenType.String || reader.ValueIsEscaped) return null;
                found = true;
                var subject = reader.ValueSpan;
                if (!subject.StartsWith("USER:EVE:"u8)) return null;
                var id = subject[9..];
                if (id.IsEmpty) return null;
                foreach (byte digit in id) if (digit < '0' || digit > '9') return null;
                if (!Utf8Parser.TryParse(id, out long number, out int consumed) || consumed != id.Length || number <= 0) return null;
                result = number;
            }
            return result;
        }
        catch (JsonException) { return null; }
        finally
        {
            Array.Clear(encoded);
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inheritHandle, int processId);
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(SafeProcessHandle process, int informationClass, IntPtr information, int length, out int returnLength);
}
