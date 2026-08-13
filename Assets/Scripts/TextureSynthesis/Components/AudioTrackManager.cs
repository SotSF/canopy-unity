using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework;
using UnityEngine;

/// <summary>
/// Scene singleton that owns AudioSource instances for AudioTrackNodes — the audio
/// counterpart to VFXRegistry. Clips come from the inspector list plus any AudioClips
/// found in a Resources subfolder (default "AudioTracks"). Sources are tracked by
/// owning node and reclaimed when the node is deleted or its canvas is unloaded.
/// </summary>
public class AudioTrackManager : Singleton<AudioTrackManager>
{
    [Tooltip("Audio clips available for playback. Merged with clips discovered in the Resources folder below.")]
    public List<AudioClip> clips = new List<AudioClip>();

    [Tooltip("Resources subfolder scanned for audio clips.")]
    public string clipResourcePath = "AudioTracks";

    [Tooltip("On canvas load, release sources whose owning node is not part of the new canvas.")]
    public bool releaseOrphansOnCanvasLoad = true;

    private Dictionary<string, AudioClip> clipsByName;
    private readonly Dictionary<Node, AudioSource> sourcesByOwner = new Dictionary<Node, AudioSource>();

    protected override void OnAwake()
    {
        RefreshClips();
        NodeEditorCallbacks.OnLoadCanvas += HandleCanvasLoaded;
    }

    private void OnDestroy()
    {
        NodeEditorCallbacks.OnLoadCanvas -= HandleCanvasLoaded;
    }

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
    }

    public string[] ClipNames
    {
        get
        {
            if (clipsByName == null) RefreshClips();
            return clipsByName.Keys.OrderBy(n => n).ToArray();
        }
    }

    public AudioClip GetClip(string name)
    {
        if (clipsByName == null) RefreshClips();
        clipsByName.TryGetValue(name, out var clip);
        return clip;
    }

    /// <summary>
    /// Creates a 2D AudioSource for the clip, tracked by owner; a node re-binding
    /// replaces its old source. Returns null if the clip is unknown.
    /// </summary>
    public AudioSource CreateSource(Node owner, string clipName)
    {
        var clip = GetClip(clipName);
        if (clip == null)
        {
            Debug.LogError($"AudioTrackManager: no clip named '{clipName}' is registered.");
            return null;
        }
        if (owner != null && sourcesByOwner.ContainsKey(owner))
        {
            ReleaseSource(owner);
        }
        var go = new GameObject($"AudioTrack: {clip.name}");
        go.transform.SetParent(transform);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        if (owner != null)
        {
            sourcesByOwner[owner] = source;
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
