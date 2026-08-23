using System;
using System.Linq;
using Lasp;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Runtime-selectable audio input device (microphones, line-ins, loopback devices...).
/// Enumerates available inputs through LASP's AudioSystem at runtime — no edit-time
/// Inspector binding — with a rescan button for hot-plugged hardware. The selected
/// device streams through a LaspDeviceCapture (AudioInputManager-owned), emitting
/// spectrumData/sampleRate on the same [0,1] dB-normalized scale as
/// SystemAudioSpectrum and AudioTrack, so MelFilterbank chains are interchangeable.
/// The device ID is saved with the canvas and rebinds on load; a missing device
/// drops back to the picker with a warning instead of failing silently.
/// </summary>
[Node(false, "Audio/AudioDevice")]
public class AudioDeviceNode : TickingNode
{
    public override string GetID => "AudioDeviceNode";
    public override string Title { get { return "AudioDevice"; } }

    private Vector2 _DefaultSize = new Vector2(250, 200);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(250, 120);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    [ValueConnectionKnob("sampleRate", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob sampleRateKnob;

    [ValueConnectionKnob("attackTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob attackTauKnob;

    [ValueConnectionKnob("releaseTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob releaseTauKnob;

    public string deviceId = "";
    public string deviceName = "";
    public bool deviceBound = false;
    public float attackTau = 0.04f;
    public float releaseTau = 0.25f;
    public float floorDb = -60f;

    [NonSerialized] private LaspDeviceCapture capture;
    [NonSerialized] private string[] listNames;
    [NonSerialized] private string[] listIds;
    [NonSerialized] private int selectedIdx;
    // GUI latches intent; the device list and bindings only change in DoCalc's
    // once-per-frame Update tick so GUI structure never mutates mid-pass
    [NonSerialized] private bool pendingRescan;
    [NonSerialized] private bool pendingBind;
    [NonSerialized] private bool pendingUnbind;
    [NonSerialized] private bool pendingReinit;
    [NonSerialized] private int lastPulseFrame = -1;

    // A LASP input stream's FIRST open on a device comes up live-but-silent; only a
    // close-and-reopen streams audio — which is exactly what a manual Stop→Capture does
    // (Stop drops the capture, LASP closes the device stream after DelayToSleep idle
    // frames, Capture reopens it). A single auto-bind is only ever a first open, so it
    // stays silent. We therefore automate Stop→wait→Capture: bind a throwaway primer,
    // release it long enough for LASP to close the stream, then bind for real. ALL
    // binds (auto-rebind and the manual picker) run through this cycle.
    const int PrimeWarmupFrames = 5;   // small settle before opening the primer
    const int PrimeHoldFrames = 5;     // let the primer stream actually open
    const int PrimeSleepFrames = 20;   // > LASP DelayToSleep (10) so the device stream closes before reopen
    const int AuthWaitTimeoutFrames = 600; // ~10s cap on holding for the OS mic prompt
    const int MaxSilentRetries = 3;    // bounded reopen attempts for a stream stuck on pure zeros
    // Prime phases: 0 idle, 1 warming/awaiting mic permission, 2 primer open,
    // 3 stream closing, 4 primed+bound
    [NonSerialized] private int primePhase = 0;
    [NonSerialized] private int primeStamp = 0;
    [NonSerialized] private int silentRetries = 0;
    // All-zeros spectrum emitted while priming/unbound so downstream never sees null
    [NonSerialized] private float[] idleSpectrum;

    public override void DoInit()
    {
        // Intentionally does NOT bind here. DoInit runs during canvas init at play
        // start (frame 0); all auto-binding (with the priming cycle below) is driven
        // from DoCalc's once-per-frame tick instead.
    }

    protected override void OnDelete()
    {
        if (Application.isPlaying && AudioInputManager.Instance != null)
        {
            AudioInputManager.Instance.ReleaseCapture(this);
        }
    }

    void BindDevice()
    {
        capture = AudioInputManager.Instance != null
            ? AudioInputManager.Instance.CreateCapture(this, deviceId)
            : null;
        if (capture == null)
        {
            Debug.LogWarning($"AudioDevice: couldn't bind '{deviceName}' ({deviceId}); returning to picker.");
            deviceBound = false;
            return;
        }
        deviceId = capture.DeviceId;
        deviceName = capture.DeviceName;
        deviceBound = true;
    }

    void UnbindDevice()
    {
        if (AudioInputManager.Instance != null)
        {
            AudioInputManager.Instance.ReleaseCapture(this);
        }
        capture = null;
        deviceBound = false;
        deviceId = "";
        deviceName = "";
    }

    // Drives the bind priming cycle once per frame. A LASP device stream's first
    // open is silent; only a close-and-reopen streams audio, so we bind a throwaway
    // primer, release it long enough for LASP to close the stream, then bind for real.
    // Both the auto-rebind and the manual picker route through this cycle.
    void AdvancePriming()
    {
        if (!deviceBound)
        {
            primePhase = 0; // in the picker (or a failed/stopped bind); nothing to prime
            return;
        }
        // NonSerialized fields survive play-mode exit with domain reload disabled; a stamp
        // from a prior session outruns the reset frame counter. Treat that as a new session
        // and re-prime from scratch, so we never resume a half-finished cycle.
        if (primeStamp > Time.frameCount)
        {
            primeStamp = Time.frameCount;
            primePhase = 0;
        }
        int elapsed = Time.frameCount - primeStamp;

        switch (primePhase)
        {
            case 0: // bound but no live capture → begin priming (also catches re-entry)
                if (capture == null) { primePhase = 1; primeStamp = Time.frameCount; }
                break;
            case 1: // await mic permission, brief settle, then open the throwaway primer
                // Instance access here also creates the manager, which kicks off the OS
                // permission request. Hold (bounded) while the prompt is pending: a
                // stream opened before the user answers stays silent even once granted.
                // On Denied we proceed anyway — the stream just delivers zeros and the
                // GUI surfaces the warning — so a wrong Denied never blocks capture.
                var mgr = AudioInputManager.Instance;
                if (mgr == null) break;
                bool authPending = mgr.MicAuth == AudioInputManager.MicAuthState.Unknown
                    || mgr.MicAuth == AudioInputManager.MicAuthState.Requesting;
                if (authPending && elapsed < AuthWaitTimeoutFrames) break;
                if (elapsed >= PrimeWarmupFrames)
                {
                    AudioSystem.RequestDeviceScan(); // enumerate fresh, like the picker does
                    BindDevice();
                    if (!deviceBound) { primePhase = 0; break; } // device missing → dropped to picker
                    primePhase = 2; primeStamp = Time.frameCount;
                }
                break;
            case 2: // hold so the primer stream actually opens, then drop it
                if (elapsed >= PrimeHoldFrames)
                {
                    if (AudioInputManager.Instance != null)
                        AudioInputManager.Instance.ReleaseCapture(this);
                    capture = null; // keep deviceBound/deviceId; just release the primer
                    primePhase = 3; primeStamp = Time.frameCount;
                }
                break;
            case 3: // wait for LASP to close the idle device stream, then bind for real
                if (elapsed >= PrimeSleepFrames)
                {
                    BindDevice();
                    primePhase = deviceBound ? 4 : 0;
                }
                break;
            case 4: // primed and streaming; recover if the capture is lost or plays dead
                if (capture == null)
                {
                    primePhase = 1;
                    primeStamp = Time.frameCount;
                }
                else if (capture.LooksSilent && silentRetries < MaxSilentRetries)
                {
                    // A stream stuck on exact zeros is dead (mic permission granted after
                    // it opened, or another bad open) — reopen it. Bounded, so a loopback
                    // device that is legitimately silent doesn't reopen forever.
                    silentRetries++;
                    Debug.LogWarning($"AudioDevice: '{deviceName}' delivering pure silence; " +
                        $"reopening ({silentRetries}/{MaxSilentRetries}).");
                    if (AudioInputManager.Instance != null)
                        AudioInputManager.Instance.ReleaseCapture(this);
                    capture = null;
                    primePhase = 3; // capture already released: skip straight to close-wait
                    primeStamp = Time.frameCount;
                }
                else if (capture.ZeroStreakFrames == 0)
                {
                    silentRetries = 0; // live signal observed → refresh the retry budget
                }
                break;
        }
    }

    void RefreshDeviceList()
    {
        AudioSystem.RequestDeviceScan();
        var devices = AudioSystem.InputDevices.ToArray();
        listNames = devices.Select(d => $"{d.Name} ({d.ChannelCount}ch {d.SampleRate}Hz)").ToArray();
        listIds = devices.Select(d => d.ID).ToArray();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        if (!deviceBound)
        {
            DrawPicker();
        }
        else
        {
            DrawCapture();
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void DrawMicAuthWarning()
    {
        switch (MicAuth)
        {
            case AudioInputManager.MicAuthState.Denied:
                GUILayout.Label("⚠ mic access denied — enable in System Settings");
                GUILayout.Label("› Privacy & Security › Microphone, then Reinit");
                break;
            case AudioInputManager.MicAuthState.Requesting:
                GUILayout.Label("awaiting mic permission…");
                break;
        }
    }

    void DrawPicker()
    {
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to pick an input device");
            return;
        }
        DrawMicAuthWarning();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Input device:");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Rescan", GUILayout.Width(56)))
        {
            pendingRescan = true;
        }
        GUILayout.EndHorizontal();

        if (listNames == null || listNames.Length == 0)
        {
            GUILayout.Label(listNames == null ? "scanning..." : "no input devices found");
        }
        else
        {
            selectedIdx = Mathf.Clamp(selectedIdx, 0, listNames.Length - 1);
            selectedIdx = GUILayout.SelectionGrid(selectedIdx, listNames, 1);
            GUILayout.Space(4);
            if (GUILayout.Button("Capture"))
            {
                pendingBind = true;
            }
        }
    }

    // OS mic permission state, without creating the manager from an edit-mode GUI pass.
    // Instance access is safe (and useful — it kicks off the permission request early)
    // only while playing.
    AudioInputManager.MicAuthState MicAuth =>
        Application.isPlaying && AudioInputManager.Instance != null
            ? AudioInputManager.Instance.MicAuth
            : AudioInputManager.MicAuthState.Unknown;

    void DrawCapture()
    {
        // Fixed width so the label region is stable as status text changes
        GUILayout.Label(deviceName, GUILayout.Width(220));
        string status =
            !Application.isPlaying ? "enter play mode to capture"
            : capture == null ? "starting…"
            : capture.DeviceLost ? "⚠ device lost"
            : capture.SampleRate == 0 ? "opening…" // stream in LASP's open-retry window
            : capture.LooksSilent ? "⚠ no signal"
            : $"{capture.SampleRate} Hz, {capture.ChannelCount} ch";
        GUILayout.BeginHorizontal();
        GUILayout.Label(status, GUILayout.Width(118));
        // && guard: an edit-mode click must not latch an action for the next play session
        if (GUILayout.Button("Reinit", GUILayout.Width(48)) && Application.isPlaying)
        {
            pendingReinit = true;
        }
        if (GUILayout.Button("Stop", GUILayout.Width(44)) && Application.isPlaying)
        {
            pendingUnbind = true;
        }
        GUILayout.EndHorizontal();
        DrawMicAuthWarning();

        spectrumDataKnob.DisplayLayout();
        sampleRateKnob.DisplayLayout();
        FloatKnobOrSlider(ref attackTau, 0f, 1f, attackTauKnob);
        FloatKnobOrSlider(ref releaseTau, 0f, 2f, releaseTauKnob);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Flr", GUILayout.Width(28));
        floorDb = RTEditorGUI.Slider(floorDb, -90f, -20f);
        GUILayout.EndHorizontal();
    }

    public override bool DoCalc()
    {
        // List/binding changes happen once per frame in the Update-phase tick, never
        // during a GUI pass (OnNodeChange can re-run Calculate mid-OnGUI)
        if (Time.frameCount != lastPulseFrame && Application.isPlaying)
        {
            lastPulseFrame = Time.frameCount;

            if (!deviceBound && listNames == null)
            {
                RefreshDeviceList(); // first idle frame populates the picker
            }
            if (pendingRescan)
            {
                RefreshDeviceList();
            }
            if (pendingBind && listIds != null && selectedIdx >= 0 && selectedIdx < listIds.Length)
            {
                // Manual picker bind routes through the priming cycle too (not a direct
                // BindDevice): a raw first open is unreliable, and in a build there is
                // no editor restart to fall back on. Phase 1 validates the device and
                // drops back to the picker if it's gone.
                deviceId = listIds[selectedIdx];
                deviceName = listNames[selectedIdx];
                deviceBound = true;
                silentRetries = 0;
                primePhase = 1;
                primeStamp = Time.frameCount;
            }
            if (pendingUnbind)
            {
                UnbindDevice();
            }
            if (pendingReinit && deviceBound)
            {
                // Clean re-initialization: drop the capture (keeping the binding), reset
                // the retry budget, re-check mic permission (the user may have just
                // granted it in System Settings), and run the full priming cycle again.
                if (AudioInputManager.Instance != null)
                {
                    AudioInputManager.Instance.ReleaseCapture(this);
                    AudioInputManager.Instance.RequestMicAuthorization();
                }
                capture = null;
                silentRetries = 0;
                primePhase = 1;
                primeStamp = Time.frameCount;
            }
            pendingRescan = pendingBind = pendingUnbind = pendingReinit = false;

            // Auto-(re)establish a saved binding via the priming cycle. This drives both:
            //  - Fresh play start: DoInit no longer binds, so the saved device binds here.
            //  - Re-entry: fast enter play mode (domain + scene reload disabled) keeps
            //    this node instance and its NonSerialized 'initialized' flag alive, so
            //    DoInit never re-runs; meanwhile the capture GameObject was destroyed on
            //    play-mode exit, leaving 'capture' a fake-null stale reference.
            // A single bind opens a silent stream (see the prime-constants comment above),
            // so we automate the manual Stop→wait→Capture that is known to fix it.
            AdvancePriming();
        }

        if (capture == null || !capture.IsRunning)
        {
            // While priming/unbound, emit silence rather than nothing: downstream
            // nodes (e.g. MelFilterbank) return false on a null spectrum, which
            // blocks the whole canvas calculation with console errors.
            if (deviceBound)
            {
                if (idleSpectrum == null) idleSpectrum = new float[LaspDeviceCapture.SpectrumResolution];
                spectrumDataKnob.SetValue(idleSpectrum);
                sampleRateKnob.SetValue(44100f);
            }
            return true;
        }
        capture.AttackTau = attackTauKnob.connected() ? attackTauKnob.GetValue<float>() : attackTau;
        capture.ReleaseTau = releaseTauKnob.connected() ? releaseTauKnob.GetValue<float>() : releaseTau;
        capture.FloorDb = floorDb;
        spectrumDataKnob.SetValue(capture.Spectrum);
        // Never publish 0 (stream still opening) — downstream nodes use this as a
        // divisor for frequency-bin math.
        sampleRateKnob.SetValue(capture.SampleRate > 0 ? capture.SampleRate : 44100f);
        return true;
    }
}
