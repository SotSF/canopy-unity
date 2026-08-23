using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lasp;
using NodeEditorFramework;
using UnityEngine;

/// <summary>
/// Scene singleton that owns LaspDeviceCapture instances for AudioDeviceNodes — the
/// audio-input counterpart to AudioTrackManager. Captures are tracked by owning node
/// and reclaimed when the node is deleted or its canvas is unloaded, so each node can
/// listen to its own input device simultaneously. Also owns the OS microphone
/// authorization handshake: on macOS the OS silently delivers zeros to unauthorized
/// processes, so consumers should hold off binding until MicAuth resolves.
/// </summary>
public class AudioInputManager : Singleton<AudioInputManager>
{
    public enum MicAuthState { Unknown, Requesting, Granted, Denied }

    /// <summary>OS microphone permission state. In builds the request triggers the
    /// system prompt (requires NSMicrophoneUsageDescription / Player Settings ›
    /// Microphone Usage Description, else macOS denies silently with zero samples).</summary>
    public MicAuthState MicAuth { get; private set; } = MicAuthState.Unknown;

    private readonly Dictionary<Node, LaspDeviceCapture> capturesByOwner = new Dictionary<Node, LaspDeviceCapture>();

    protected override void OnAwake()
    {
        NodeEditorCallbacks.OnLoadCanvas += HandleCanvasLoaded;
        RequestMicAuthorization();
    }

    /// <summary>Kick off (or re-check) OS microphone authorization. Safe to call
    /// repeatedly — e.g. from a Reinit after the user grants access in System Settings.</summary>
    public void RequestMicAuthorization()
    {
        if (MicAuth == MicAuthState.Requesting) return;
        StartCoroutine(MicAuthRoutine());
    }

    IEnumerator MicAuthRoutine()
    {
        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            MicAuth = MicAuthState.Granted;
            yield break;
        }
        MicAuth = MicAuthState.Requesting;
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        MicAuth = Application.HasUserAuthorization(UserAuthorization.Microphone)
            ? MicAuthState.Granted : MicAuthState.Denied;
        if (MicAuth == MicAuthState.Denied)
        {
            Debug.LogWarning("AudioInputManager: microphone authorization denied — input " +
                "devices will deliver silence. Grant access in System Settings › Privacy " +
                "& Security › Microphone, then Reinit the AudioDevice node.");
        }
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
        // Bind can still throw from deep in LASP (e.g. a device handle disposed
        // mid-bind by a rescan); treat that as a failed bind rather than leaking
        // the half-built GameObject and unwinding the caller's state machine.
        bool bound;
        try
        {
            bound = capture.Bind(deviceId);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            bound = false;
        }
        if (!bound)
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
