using System.Collections.Generic;
using System.Linq;
using Lasp;
using NodeEditorFramework;
using UnityEngine;

/// <summary>
/// Scene singleton that owns LaspDeviceCapture instances for AudioDeviceNodes — the
/// audio-input counterpart to AudioTrackManager. Captures are tracked by owning node
/// and reclaimed when the node is deleted or its canvas is unloaded, so each node can
/// listen to its own input device simultaneously.
/// </summary>
public class AudioInputManager : Singleton<AudioInputManager>
{
    private readonly Dictionary<Node, LaspDeviceCapture> capturesByOwner = new Dictionary<Node, LaspDeviceCapture>();

    protected override void OnAwake()
    {
        NodeEditorCallbacks.OnLoadCanvas += HandleCanvasLoaded;
    }

    private void OnDestroy()
    {
        NodeEditorCallbacks.OnLoadCanvas -= HandleCanvasLoaded;
    }

    /// <summary>
    /// Creates a capture bound to the given device (null/empty = default input),
    /// tracked by owner; a node re-binding replaces its old capture. Null on failure.
    /// </summary>
    public LaspDeviceCapture CreateCapture(Node owner, string deviceId)
    {
        if (owner != null && capturesByOwner.ContainsKey(owner))
        {
            ReleaseCapture(owner);
        }
        var go = new GameObject("AudioInput (binding...)");
        go.transform.SetParent(transform);
        var capture = go.AddComponent<LaspDeviceCapture>();
        if (!capture.Bind(deviceId))
        {
            Destroy(go);
            return null;
        }
        go.name = $"AudioInput: {capture.DeviceName}";
        if (owner != null)
        {
            capturesByOwner[owner] = capture;
        }
        return capture;
    }

    public void ReleaseCapture(Node owner)
    {
        if (owner == null || !capturesByOwner.TryGetValue(owner, out var capture)) return;
        capturesByOwner.Remove(owner);
        if (capture != null)
        {
            Destroy(capture.gameObject);
        }
    }

    private void HandleCanvasLoaded(NodeCanvas canvas)
    {
        if (canvas == null) return;
        var orphans = capturesByOwner.Keys
            .Where(node => node == null || !canvas.nodes.Contains(node))
            .ToList();
        foreach (var node in orphans)
        {
            ReleaseCapture(node);
        }
    }
}
