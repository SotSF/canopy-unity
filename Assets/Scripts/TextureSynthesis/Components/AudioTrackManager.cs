using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NodeEditorFramework;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Scene singleton that owns AudioSource instances for AudioTrackNodes — the audio
/// counterpart to VFXRegistry. Clips come from the inspector list, any AudioClips found
/// in a Resources subfolder (default "AudioTracks"), and audio files dropped into
/// Files/Audio/ beside the player (see FolderUtil.GameFilesDir). Project clips are named
/// by asset name; disk clips by file name WITH extension, so the two never collide and a
/// node serializes just one name string. Disk clips decode asynchronously through
/// UnityWebRequest (kept compressed in memory: loads in milliseconds, decodes during
/// playback, seeks and spectrum analysis still work) and are cached for the session.
/// Sources are tracked by owning node and reclaimed when the node is deleted or its
/// canvas is unloaded.
/// </summary>
public class AudioTrackManager : Singleton<AudioTrackManager>
{
    public enum ClipStatus { Unknown, Loading, Ready, Failed }

    [Tooltip("Audio clips available for playback. Merged with clips discovered in the Resources folder below.")]
    public List<AudioClip> clips = new List<AudioClip>();

    [Tooltip("Resources subfolder scanned for audio clips.")]
    public string clipResourcePath = "AudioTracks";

    [Tooltip("On canvas load, release sources whose owning node is not part of the new canvas.")]
    public bool releaseOrphansOnCanvasLoad = true;

    public const string DiskFolderName = "Audio";

    static readonly Dictionary<string, AudioType> DiskAudioTypes = new Dictionary<string, AudioType>
    {
        { ".mp3", AudioType.MPEG },
        { ".wav", AudioType.WAV },
        { ".ogg", AudioType.OGGVORBIS },
        { ".aif", AudioType.AIFF },
        { ".aiff", AudioType.AIFF },
    };

    private Dictionary<string, AudioClip> clipsByName;
    private string[] diskNames = new string[0];
    private readonly Dictionary<string, AudioClip> diskClips = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> failedDiskClips = new HashSet<string>();
    // Sources waiting on an in-flight decode, keyed by clip name; a key's presence means
    // a request is in flight
    private readonly Dictionary<string, List<AudioSource>> pendingSources = new Dictionary<string, List<AudioSource>>();
    private readonly Dictionary<Node, AudioSource> sourcesByOwner = new Dictionary<Node, AudioSource>();

    public string DiskDir => FolderUtil.GameFilesDir(DiskFolderName);

    protected override void OnAwake()
    {
        try
        {
            // Created up front so "Open dir" always has somewhere to land
            Directory.CreateDirectory(DiskDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"AudioTrackManager: couldn't create '{DiskDir}': {e.Message}");
        }
        RefreshClips();
        NodeEditorCallbacks.OnLoadCanvas += HandleCanvasLoaded;
    }

    private void OnDestroy()
    {
        NodeEditorCallbacks.OnLoadCanvas -= HandleCanvasLoaded;
        foreach (var clip in diskClips.Values)
        {
            if (clip != null) Destroy(clip);
        }
        diskClips.Clear();
    }

    /// <summary>Rescans project clips and the disk folder. Already-decoded disk clips stay
    /// cached; a failed one gets another chance on its next request.</summary>
    public void RefreshClips()
    {
        clipsByName = new Dictionary<string, AudioClip>();
        foreach (var clip in clips)
        {
            if (clip != null)
            {
                clipsByName[clip.name] = clip;
            }
        }
        foreach (var clip in Resources.LoadAll<AudioClip>(clipResourcePath))
        {
            if (!clipsByName.ContainsKey(clip.name))
            {
                clipsByName[clip.name] = clip;
            }
        }
        failedDiskClips.Clear();
        RefreshDiskNames();
    }

    void RefreshDiskNames()
    {
        try
        {
            diskNames = Directory.Exists(DiskDir)
                ? Directory.GetFiles(DiskDir)
                    .Where(p => DiskAudioTypes.ContainsKey(Path.GetExtension(p).ToLowerInvariant()))
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : new string[0];
        }
        catch (Exception e)
        {
            Debug.LogError($"AudioTrackManager: failed to list '{DiskDir}': {e.Message}");
            diskNames = new string[0];
        }
    }

    /// <summary>Project clips (sorted), then disk files (sorted).</summary>
    public string[] ClipNames
    {
        get
        {
            if (clipsByName == null) RefreshClips();
            return clipsByName.Keys.OrderBy(n => n).Concat(diskNames).ToArray();
        }
    }

    public bool IsDiskClip(string name) => !string.IsNullOrEmpty(name)
        && (clipsByName == null || !clipsByName.ContainsKey(name))
        && DiskAudioTypes.ContainsKey(Path.GetExtension(name).ToLowerInvariant());

    /// <summary>The clip if it's in memory: any project clip, or an already-decoded disk clip.</summary>
    public AudioClip GetClip(string name)
    {
        if (clipsByName == null) RefreshClips();
        if (string.IsNullOrEmpty(name)) return null;
        if (clipsByName.TryGetValue(name, out var clip)) return clip;
        diskClips.TryGetValue(name, out clip);
        return clip;
    }

    public ClipStatus GetStatus(string name)
    {
        if (GetClip(name) != null) return ClipStatus.Ready;
        if (string.IsNullOrEmpty(name)) return ClipStatus.Unknown;
        if (pendingSources.ContainsKey(name)) return ClipStatus.Loading;
        if (failedDiskClips.Contains(name)) return ClipStatus.Failed;
        return ClipStatus.Unknown;
    }

    /// <summary>
    /// Creates a 2D AudioSource for the clip, tracked by owner; a node re-binding replaces
    /// its old source. A disk clip that isn't decoded yet starts loading and the source's
    /// clip is assigned when it lands — poll source.clip (or GetStatus) before playing.
    /// Returns null if the name matches no project clip or readable disk file.
    /// </summary>
    public AudioSource CreateSource(Node owner, string clipName)
    {
        var clip = GetClip(clipName);
        bool fromDisk = clip == null && IsDiskClip(clipName) && File.Exists(PathFor(clipName));
        if (clip == null && !fromDisk)
        {
            Debug.LogError($"AudioTrackManager: no clip named '{clipName}' in Resources/{clipResourcePath} or {DiskDir}.");
            return null;
        }
        if (owner != null && sourcesByOwner.ContainsKey(owner))
        {
            ReleaseSource(owner);
        }
        var go = new GameObject($"AudioTrack: {clipName}");
        go.transform.SetParent(transform);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        if (owner != null)
        {
            sourcesByOwner[owner] = source;
        }
        if (fromDisk)
        {
            LoadDiskClip(clipName, source);
        }
        return source;
    }

    public void ReleaseSource(Node owner)
    {
        if (owner == null || !sourcesByOwner.TryGetValue(owner, out var source)) return;
        sourcesByOwner.Remove(owner);
        if (source != null)
        {
            Destroy(source.gameObject);
        }
    }

    public void OpenDiskFolder()
    {
        FolderUtil.Reveal(DiskDir);
    }

    // ---------------------------------------------------------------- disk loading

    string PathFor(string name) => Path.Combine(DiskDir, name);

    void LoadDiskClip(string name, AudioSource waiting)
    {
        if (pendingSources.TryGetValue(name, out var waiters))
        {
            waiters.Add(waiting);   // already in flight: piggyback on that decode
            return;
        }
        // Names come from serialized canvases: refuse anything that would escape the folder
        if (Path.GetFileName(name) != name)
        {
            Debug.LogError($"AudioTrackManager: refusing clip name '{name}' (not a plain file name).");
            failedDiskClips.Add(name);
            return;
        }
        failedDiskClips.Remove(name);
        pendingSources[name] = new List<AudioSource> { waiting };
        // Keep the encoded data in memory and decode during playback: a full-PCM decode of
        // a 3-minute MP3 blocks the main thread for ~0.4s in GetContent
        SendClipRequest(name, compressed: true, clip => OnDiskClipLoaded(name, clip));
    }

    void OnDiskClipLoaded(string name, AudioClip clip)
    {
        pendingSources.TryGetValue(name, out var waiters);
        pendingSources.Remove(name);
        if (clip == null)
        {
            failedDiskClips.Add(name);
            return;
        }
        diskClips[name] = clip;
        if (waiters == null) return;
        foreach (var source in waiters)
        {
            // Released (node deleted / rebound) while the decode was in flight
            if (source != null) source.clip = clip;
        }
    }

    /// <summary>
    /// Decodes a separate, fully decompressed copy of a disk clip and hands it to onLoaded
    /// (null on failure), for callers that need raw samples via GetData — the shared
    /// playback clip stays compressed, which GetData can't read. The caller owns the copy
    /// and must Destroy it. Costs a main-thread decode (~0.4s per 3 minutes of audio).
    /// </summary>
    public void RequestDecompressedCopy(string name, Action<AudioClip> onLoaded)
    {
        if (!IsDiskClip(name) || Path.GetFileName(name) != name || !File.Exists(PathFor(name)))
        {
            onLoaded(null);
            return;
        }
        SendClipRequest(name, compressed: false, onLoaded);
    }

    void SendClipRequest(string name, bool compressed, Action<AudioClip> onDone)
    {
        var type = DiskAudioTypes[Path.GetExtension(name).ToLowerInvariant()];
        var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(PathFor(name)).AbsoluteUri, type);
        var handler = (DownloadHandlerAudioClip)request.downloadHandler;
        // Order matters: streamAudio defaults to true, and setting compressed while it's
        // still on logs "Audio compression is not valid as streamAudio is set to true"
        handler.streamAudio = false;
        handler.compressed = compressed;
        request.SendWebRequest().completed += _ =>
        {
            AudioClip clip = null;
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    clip = DownloadHandlerAudioClip.GetContent(request);
                    // Undecodable data yields null here; FMOD's own log doesn't name the file
                    if (clip == null) Debug.LogError($"AudioTrackManager: '{name}' isn't a decodable audio file.");
                }
                else
                {
                    Debug.LogError($"AudioTrackManager: failed to read '{name}': {request.error}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"AudioTrackManager: failed to decode '{name}': {e.Message}");
            }
            finally
            {
                request.Dispose();
            }
            if (clip != null && clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogError($"AudioTrackManager: '{name}' isn't a decodable audio file.");
                Destroy(clip);
                clip = null;
            }
            // The manager was torn down mid-load (play mode exited): nothing left to hand it to
            if (this == null)
            {
                if (clip != null) Destroy(clip);
                return;
            }
            if (clip != null) clip.name = name;
            onDone(clip);
        };
    }

    private void HandleCanvasLoaded(NodeCanvas canvas)
    {
        if (!releaseOrphansOnCanvasLoad || canvas == null) return;
        var orphans = sourcesByOwner.Keys
            .Where(node => node == null || !canvas.nodes.Contains(node))
            .ToList();
        foreach (var node in orphans)
        {
            ReleaseSource(node);
        }
    }
}
