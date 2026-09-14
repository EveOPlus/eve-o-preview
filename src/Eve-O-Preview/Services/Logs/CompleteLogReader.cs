#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace EveOPreview.Services.Logs;

public sealed record LogCursor(string Identity, long Generation, long Offset, string Encoding,
    string Anchor, bool Dropping = false);
public sealed record CompleteLogLine(long Offset, string Text);
public sealed record LogReadBatch(LogCursor Cursor, IReadOnlyList<CompleteLogLine> Lines,
    bool More, int OversizedLines);

/// <summary>Runs on the ingestion worker, never on a UI or filesystem callback thread.</summary>
public static class CompleteLogReader
{
    public const int MaximumLineBytes = 64 * 1024;
    public const int MaximumBatchBytes = 1024 * 1024;

    public static FileStream OpenShared(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);

    public static string Identity(FileStream stream)
    {
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info))
            throw new IOException("Cannot obtain log file identity.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        // The creation time protects against reuse of an NTFS file index after deletion.
        return $"{info.Volume:x8}:{info.IndexHigh:x8}{info.IndexLow:x8}:{info.CreationHigh:x8}{info.CreationLow:x8}";
    }

    public static LogReadBatch Read(FileStream stream, string identity, LogCursor? previous)
    {
        long length = stream.Length;
        bool reset = previous is null || previous.Identity != identity || previous.Offset > length
            || !AnchorMatches(stream, previous);
        var cursor = reset ? new LogCursor(identity, previous?.Generation + 1 ?? 0, 0, "", "") : previous!;
        long start = cursor.Offset;
        string encoding = cursor.Encoding;
        if (start == 0)
        {
            stream.Position = 0;
            Span<byte> bom = stackalloc byte[3];
            int count = stream.Read(bom);
            if (count == 0 || (bom[0] == 0xEF && count < 3) || (bom[0] is 0xFF or 0xFE && count < 2))
                return new(cursor, Array.Empty<CompleteLogLine>(), false, 0);
            if (count >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) { encoding = "utf-16"; start = 2; }
            else if (count >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) { encoding = "utf-16BE"; start = 2; }
            else { encoding = "utf-8"; if (count >= 3 && bom.SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF })) start = 3; }
        }
        bool wide = encoding != "utf-8", bigEndian = encoding == "utf-16BE";
        int width = wide ? 2 : 1;
        int bytes = (int)Math.Min(MaximumBatchBytes, Math.Max(0, length - start));
        if (wide) bytes -= bytes % 2;
        byte[] buffer = new byte[bytes];
        stream.Position = start;
        int read = 0;
        while (read < bytes)
        {
            int count = stream.Read(buffer, read, bytes - read);
            if (count == 0) break;
            read += count;
        }
        if (wide) read -= read % 2;
        Encoding decoder = wide ? new UnicodeEncoding(bigEndian, false, true) : new UTF8Encoding(false, true);
        var lines = new List<CompleteLogLine>();
        int lineStart = 0, committed = 0, oversized = 0;
        bool dropping = cursor.Dropping;
        for (int i = 0; i < read; i += width)
        {
            bool newline = wide ? buffer[i + (bigEndian ? 1 : 0)] == 10 && buffer[i + (bigEndian ? 0 : 1)] == 0 : buffer[i] == 10;
            if (!newline) continue;
            int lineBytes = i - lineStart;
            if (!dropping && lineBytes <= MaximumLineBytes)
            {
                try { lines.Add(new(start + lineStart, decoder.GetString(buffer, lineStart, lineBytes).TrimEnd('\r').TrimStart('\uFEFF'))); }
                catch (DecoderFallbackException) { oversized++; /* Never emit a corrupted or incomplete character. */ }
            }
            else if (!dropping) oversized++;
            dropping = false;
            committed = lineStart = i + width;
        }
        // Bound malformed unterminated lines. Keep skipping until a real newline;
        // a suffix of a discarded line must never become an entry.
        if (read - lineStart > MaximumLineBytes || dropping)
        {
            if (!dropping) oversized++;
            dropping = true; committed = read;
        }
        long offset = start + committed;
        cursor = cursor with { Offset = offset, Encoding = encoding, Dropping = dropping, Anchor = ReadAnchor(stream, offset) };
        return new(cursor, lines, offset < length && read == MaximumBatchBytes, oversized);
    }

    private static bool AnchorMatches(FileStream stream, LogCursor cursor) => cursor.Offset == 0
        || cursor.Anchor == ReadAnchor(stream, cursor.Offset);

    private static string ReadAnchor(FileStream stream, long offset)
    {
        int count = (int)Math.Min(64, offset);
        stream.Position = offset - count;
        byte[] bytes = new byte[count];
        stream.ReadExactly(bytes);
        return Convert.ToBase64String(bytes);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
}
