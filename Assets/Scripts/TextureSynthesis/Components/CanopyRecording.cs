using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Flat-file format for captured Canopy output (.canrec): a 32-byte header, fixed-size
/// raw RGB24 frames (random access = header + index * frameSize), and a float32
/// timestamp-per-frame footer. Frames are recorded at capture rate (real timestamps,
/// not resampled), so playback maps time -> frame via the timestamp table. Fixed-size
/// raw frames are what make punch-in editing possible: overwriting a section is a
/// seek + write over exactly the frames it covers, leaving the rest byte-identical.
///
/// Header layout (little-endian):
///   0  magic       "CNRC"
///   4  version     int32 = 1
///   8  width       int32
///  12  height      int32
///  16  nominalFps  float32 (capture cap, informational)
///  20  frameCount  int32   (0 until flushed; reader infers from file size as
///                           crash recovery, synthesizing nominal-rate timestamps)
///  24  compression int32   (0 = raw RGB24; reserved for future per-frame deflate)
///  28  reserved    int32
/// </summary>
public static class CanopyRecordingFormat
{
    public const int HeaderSize = 32;
    public const int Version = 1;
    public static readonly byte[] Magic = { (byte)'C', (byte)'N', (byte)'R', (byte)'C' };
    public const string Extension = ".canrec";

    public static int FrameSize(int width, int height) => width * height * 3;
}

/// <summary>
/// Read/write access to one recording. Reads stream frames from disk on demand (memory
/// holds only the timestamp table); writes overwrite frames in place (punch-in) or
/// append past the end. Flush() persists the timestamp footer and frame count without
/// closing, so a file stays live for playback across record punches; Dispose flushes.
/// </summary>
public sealed class CanopyRecordingFile : IDisposable
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float NominalFps { get; private set; }
    public int FrameCount => timestamps.Count;
    /// <summary>End of the last frame's display interval (last timestamp + one nominal frame).</summary>
    public float Duration => timestamps.Count > 0 ? timestamps[timestamps.Count - 1] + 1f / NominalFps : 0f;
    public string Path { get; private set; }
    public bool Writable { get; private set; }
    public int FrameSize => frameSize;

    private FileStream stream;
    private List<float> timestamps = new List<float>();
    private int frameSize;
    private int lastIndexHint;
    private bool dirty;

    private CanopyRecordingFile() { }

    public static CanopyRecordingFile CreateNew(string path, int width, int height, float nominalFps)
    {
        var file = new CanopyRecordingFile
        {
            Width = width,
            Height = height,
            NominalFps = Mathf.Max(nominalFps, 1f),
            Path = path,
            Writable = true,
            frameSize = CanopyRecordingFormat.FrameSize(width, height),
        };
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        file.stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        var header = new byte[CanopyRecordingFormat.HeaderSize];
        Array.Copy(CanopyRecordingFormat.Magic, header, 4);
        BitConverter.GetBytes(CanopyRecordingFormat.Version).CopyTo(header, 4);
        BitConverter.GetBytes(width).CopyTo(header, 8);
        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(file.NominalFps).CopyTo(header, 16);
        // frameCount (20), compression (24), reserved (28) stay 0
        file.stream.Write(header, 0, header.Length);
        file.dirty = true;
        return file;
    }

    public static CanopyRecordingFile Open(string path, bool writable)
    {
        var file = new CanopyRecordingFile { Path = path, Writable = writable };
        file.stream = new FileStream(path, FileMode.Open,
            writable ? FileAccess.ReadWrite : FileAccess.Read,
            writable ? FileShare.Read : FileShare.ReadWrite);
        var header = new byte[CanopyRecordingFormat.HeaderSize];
        if (file.stream.Read(header, 0, header.Length) != header.Length)
            throw new IOException($"'{path}' is too short to be a canopy recording");
        for (int i = 0; i < 4; i++)
        {
            if (header[i] != CanopyRecordingFormat.Magic[i])
                throw new IOException($"'{path}' is not a canopy recording (bad magic)");
        }
        file.Width = BitConverter.ToInt32(header, 8);
        file.Height = BitConverter.ToInt32(header, 12);
        file.NominalFps = Mathf.Max(BitConverter.ToSingle(header, 16), 1f);
        file.frameSize = CanopyRecordingFormat.FrameSize(file.Width, file.Height);
        int frameCount = BitConverter.ToInt32(header, 20);

        if (frameCount > 0)
        {
            var footer = new byte[frameCount * 4];
            file.stream.Seek(CanopyRecordingFormat.HeaderSize + (long)frameCount * file.frameSize, SeekOrigin.Begin);
            var times = new float[frameCount];
            if (file.stream.Read(footer, 0, footer.Length) == footer.Length)
            {
                Buffer.BlockCopy(footer, 0, times, 0, footer.Length);
            }
            else
            {
                for (int i = 0; i < frameCount; i++) times[i] = i / file.NominalFps;
                file.dirty = writable;
            }
            file.timestamps.AddRange(times);
        }
        else
        {
            // Unfinalized (crashed mid-recording): infer count from size, nominal-rate times
            int inferred = (int)((file.stream.Length - CanopyRecordingFormat.HeaderSize) / file.frameSize);
            for (int i = 0; i < inferred; i++) file.timestamps.Add(i / file.NominalFps);
            file.dirty = writable && inferred > 0;
        }
        return file;
    }

    /// <summary>Reads frame `index` into `rgb` (must be width*height*3 bytes).</summary>
    public bool ReadFrame(int index, byte[] rgb)
    {
        if (stream == null || index < 0 || index >= FrameCount || rgb.Length != frameSize) return false;
        stream.Seek(CanopyRecordingFormat.HeaderSize + (long)index * frameSize, SeekOrigin.Begin);
        int read = 0;
        while (read < frameSize)
        {
            int got = stream.Read(rgb, read, frameSize - read);
            if (got <= 0) return false;
            read += got;
        }
        return true;
    }

    /// <summary>Punch-in: overwrites an existing frame's content in place. Its timestamp
    /// (and everything around it) is untouched.</summary>
    public bool WriteFrame(int index, byte[] rgb)
    {
        if (stream == null || !Writable || index < 0 || index >= FrameCount || rgb.Length != frameSize) return false;
        stream.Seek(CanopyRecordingFormat.HeaderSize + (long)index * frameSize, SeekOrigin.Begin);
        stream.Write(rgb, 0, frameSize);
        dirty = true;
        return true;
    }

    /// <summary>Appends a frame past the current end. Timestamps must stay monotonic;
    /// out-of-order times are clamped forward. Overwrites any stale footer bytes.</summary>
    public bool AppendFrame(byte[] rgb, float timestamp)
    {
        if (stream == null || !Writable || rgb.Length != frameSize) return false;
        if (timestamps.Count > 0 && timestamp <= timestamps[timestamps.Count - 1])
            timestamp = timestamps[timestamps.Count - 1] + 1f / NominalFps;
        stream.Seek(CanopyRecordingFormat.HeaderSize + (long)FrameCount * frameSize, SeekOrigin.Begin);
        stream.Write(rgb, 0, frameSize);
        timestamps.Add(timestamp);
        dirty = true;
        return true;
    }

    /// <summary>Index of the frame displayed at time t (last frame whose timestamp &lt;= t).
    /// Sequential playback is O(1) via a moving hint; seeks fall back to binary search.</summary>
    public int FrameIndexAtTime(float t)
    {
        if (FrameCount == 0) return -1;
        if (t <= timestamps[0]) return 0;
        if (t >= timestamps[FrameCount - 1]) return FrameCount - 1;
        // fast path: playhead advanced a little since last query
        int h = Mathf.Clamp(lastIndexHint, 0, FrameCount - 2);
        if (timestamps[h] <= t)
        {
            for (int step = 0; step < 8 && h + 1 < FrameCount; step++, h++)
            {
                if (timestamps[h + 1] > t) { lastIndexHint = h; return h; }
            }
        }
        int lo = 0, hi = FrameCount - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (timestamps[mid] <= t) lo = mid;
            else hi = mid - 1;
        }
        lastIndexHint = lo;
        return lo;
    }

    /// <summary>Persists the timestamp footer + frame count without closing, so the file
    /// survives a crash after a punch while staying live for playback.</summary>
    public void Flush()
    {
        if (stream == null || !Writable || !dirty) return;
        var footer = new byte[timestamps.Count * 4];
        Buffer.BlockCopy(timestamps.ToArray(), 0, footer, 0, footer.Length);
        stream.Seek(CanopyRecordingFormat.HeaderSize + (long)FrameCount * frameSize, SeekOrigin.Begin);
        stream.Write(footer, 0, footer.Length);
        // Punch-only edits shrink nothing, but a reopened crash file may be shorter than
        // its old footer suggests; truncate to the exact valid length.
        stream.SetLength(stream.Position);
        stream.Seek(20, SeekOrigin.Begin);
        stream.Write(BitConverter.GetBytes(timestamps.Count), 0, 4);
        stream.Flush();
        dirty = false;
    }

    public void Dispose()
    {
        if (stream == null) return;
        Flush();
        stream.Dispose();
        stream = null;
    }
}
