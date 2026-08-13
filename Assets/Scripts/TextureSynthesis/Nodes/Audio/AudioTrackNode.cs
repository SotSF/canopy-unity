using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

/// <summary>
/// Plays a pre-recorded audio clip (from AudioTrackManager: inspector list or a
/// Resources/AudioTracks folder) through the system audio device, with transport +
/// scrub controls, and outputs its live spectrum for downstream analysis
/// (MelFilterbank → SpectrumVisualizer etc.), plus the current playback time.
/// The syncTime input drift-corrects playback to an external clock, e.g. a
/// TimelineNode's time output, for sample-accurate synced visuals.
/// </summary>
[Node(false, "Audio/AudioTrack")]
public class AudioTrackNode : TickingNode
{
    public override string GetID => "AudioTrackNode";
    public override string Title { get { return "AudioTrack"; } }

    private Vector2 _DefaultSize = new Vector2(250, 260);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    [ValueConnectionKnob("sampleRate", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob sampleRateKnob;

    [ValueConnectionKnob("time", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob timeKnob;

    [ValueConnectionKnob("syncTime", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob syncTimeKnob;

    [ValueConnectionKnob("playPause", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob playPauseKnob;

    const int SpectrumSize = 2048;
    const float SyncDriftThreshold = 0.15f; // seconds of drift before a corrective seek

    public string clipName = "";
    public bool trackBound = false;
    public bool playing = false;
    public bool loop = false;
    public float volume = 1f;
    public float attackTau = 0.04f;
    public float releaseTau = 0.25f;
    // dB normalization window, matching SystemAudioCapture/LASP so both audio chains
    // produce comparable [0,1] spectra (raw FFT magnitudes are linear and tiny)
    public float floorDb = -60f;
    public float headDb = 0f;
    public float savedTime = 0f; // playback position restored on canvas load

    [NonSerialized] AudioSource source;
    [NonSerialized] float[] rawSpectrum;
    [NonSerialized] float[] smoothedSpectrum;
    [NonSerialized] int selectedClipIdx = 0;
    [NonSerialized] bool scrubbing = false;

    public override void DoInit()
    {
        if (rawSpectrum == null) rawSpectrum = new float[SpectrumSize];
        if (smoothedSpectrum == null) smoothedSpectrum = new float[SpectrumSize];
        if (trackBound && Application.isPlaying)
        {
            BindClip();
        }
    }

    void BindClip()
    {
        source = AudioTrackManager.Instance.CreateSource(this, clipName);
        if (source == null)
        {
            Debug.LogError($"AudioTrackNode: failed to bind clip '{clipName}', unbinding.");
            UnbindClip();
            return;
        }
        source.loop = loop;
        source.volume = volume;
        source.time = Mathf.Clamp(savedTime, 0f, Mathf.Max(source.clip.length - 0.05f, 0f));
        if (playing) source.Play();
    }

    void UnbindClip()
    {
        trackBound = false;
        clipName = "";
        playing = false;
        savedTime = 0f;
        if (Application.isPlaying && AudioTrackManager.Instance != null)
        {
            AudioTrackManager.Instance.ReleaseSource(this);
        }
        source = null;
    }

    protected override void OnDelete()
    {
        if (trackBound && Application.isPlaying && AudioTrackManager.Instance != null)
        {
            AudioTrackManager.Instance.ReleaseSource(this);
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        if (!trackBound)
        {
            DrawClipPicker();
        }
        else
        {
            DrawTransport();
        }

        GUILayout.BeginHorizontal();
        playPauseKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        syncTimeKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        spectrumDataKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        sampleRateKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        timeKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void DrawClipPicker()
    {
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to bind a track");
            return;
        }
        var names = AudioTrackManager.Instance.ClipNames;
        if (names.Length == 0)
        {
            GUILayout.Label("No clips found (Resources/AudioTracks)");
            return;
        }
        GUILayout.Label("Track:");
        selectedClipIdx = Mathf.Clamp(selectedClipIdx, 0, names.Length - 1);
        selectedClipIdx = GUILayout.SelectionGrid(selectedClipIdx, names, 1);
        GUILayout.Space(6);
        if (GUILayout.Button("Load Track"))
        {
            clipName = names[selectedClipIdx];
            trackBound = true;
            savedTime = 0f;
            BindClip();
        }
    }

    void DrawTransport()
    {
        GUILayout.Label(clipName);
        GUILayout.BeginHorizontal();
        bool sourceAlive = source != null && source.clip != null;
        if (GUILayout.Button(playing ? "❚❚" : "▶", GUILayout.Width(30)))
        {
            TogglePlay();
        }
        if (GUILayout.Button("⏮", GUILayout.Width(30)) && sourceAlive)
        {
            source.time = 0f;
        }
        loop = GUILayout.Toggle(loop, "Loop", GUI.skin.button, GUILayout.Width(44));
        if (GUILayout.Button("Unbind", GUILayout.Width(52)))
        {
            UnbindClip();
            GUILayout.EndHorizontal();
            return;
        }
        if (sourceAlive)
        {
            GUILayout.Label($"{FormatTime(source.time)} / {FormatTime(source.clip.length)}");
        }
        GUILayout.EndHorizontal();

        // scrub bar: click or drag to seek
        Rect bar = GUILayoutUtility.GetRect(10f, 14f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint && sourceAlive)
        {
            FillRect(bar, new Color(0.07f, 0.07f, 0.07f, 1f));
            float frac = source.clip.length > 0f ? source.time / source.clip.length : 0f;
            FillRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(frac), bar.height),
                new Color(0.40f, 0.85f, 1.00f, 0.55f));
        }
        var e = Event.current;
        if (sourceAlive && e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
        {
            scrubbing = true;
            Seek(bar, e.mousePosition.x);
            e.Use();
        }
        else if (scrubbing && e.type == EventType.MouseDrag)
        {
            if (sourceAlive) Seek(bar, e.mousePosition.x);
            e.Use();
        }
        else if (scrubbing && e.type == EventType.MouseUp)
        {
            scrubbing = false;
            e.Use();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Vol", GUILayout.Width(28));
        volume = RTEditorGUI.Slider(volume, 0f, 1f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Atk", GUILayout.Width(28));
        attackTau = RTEditorGUI.Slider(attackTau, 0f, 1f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Rel", GUILayout.Width(28));
        releaseTau = RTEditorGUI.Slider(releaseTau, 0f, 2f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Flr", GUILayout.Width(28));
        floorDb = RTEditorGUI.Slider(floorDb, -90f, -20f);
        GUILayout.EndHorizontal();
    }

    void TogglePlay()
    {
        playing = !playing;
        if (source == null) return;
        if (playing) source.Play();
        else source.Pause();
    }

    void Seek(Rect bar, float mouseX)
    {
        float frac = Mathf.Clamp01((mouseX - bar.x) / Mathf.Max(bar.width, 1f));
        source.time = frac * Mathf.Max(source.clip.length - 0.05f, 0f);
    }

    static string FormatTime(float t)
    {
        long tenths = (long)Mathf.Round(Mathf.Max(t, 0f) * 10f);
        long mins = tenths / 600;
        return $"{mins}:{(tenths % 600) / 10f:00.0}";
    }

    static void FillRect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public override bool DoCalc()
    {
        if (rawSpectrum == null) rawSpectrum = new float[SpectrumSize];
        if (smoothedSpectrum == null) smoothedSpectrum = new float[SpectrumSize];

        if (!trackBound || source == null || source.clip == null)
        {
            spectrumDataKnob?.SetValue(smoothedSpectrum);
            sampleRateKnob?.SetValue((float)AudioSettings.outputSampleRate);
            timeKnob?.SetValue(0f);
            return true;
        }

        source.loop = loop;
        source.volume = volume;

        // one-frame event pulse toggles play/pause (wire a Timeline event port here)
        if (playPauseKnob != null && playPauseKnob.connected() && playPauseKnob.GetValue<bool>())
        {
            TogglePlay();
        }

        // a non-looping track that reached its end stops itself
        if (playing && !source.isPlaying && !scrubbing)
        {
            playing = false;
        }

        // drift-correct against an external clock (e.g. a TimelineNode's time output)
        if (syncTimeKnob != null && syncTimeKnob.connected())
        {
            float target = syncTimeKnob.GetValue<float>();
            if (target >= 0f && target <= source.clip.length &&
                Mathf.Abs(source.time - target) > SyncDriftThreshold)
            {
                source.time = target;
            }
        }

        source.GetSpectrumData(rawSpectrum, 0, FFTWindow.BlackmanHarris);

        // dB-normalize to [0,1] over [floorDb, headDb], replicating LASP's FftBuffer
        // postprocess (dBFS reference = full-scale sine = 1/sqrt(2)) so this spectrum
        // is scale-compatible with SystemAudioSpectrum's; then per-bin attack/release,
        // frame-rate independent, applied post-normalization like SystemAudioCapture.
        float dbRange = Mathf.Max(headDb - floorDb, 1f);
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float attackAlpha = attackTau > 1e-4f ? 1f - Mathf.Exp(-dt / attackTau) : 1f;
        float releaseAlpha = releaseTau > 1e-4f ? 1f - Mathf.Exp(-dt / releaseTau) : 1f;
        for (int i = 0; i < SpectrumSize; i++)
        {
            float db = 20f * Mathf.Log10(rawSpectrum[i] / 0.7071f + 1.5849e-13f);
            float target = (db - floorDb) / dbRange;
            float current = smoothedSpectrum[i];
            float alpha = target > current ? attackAlpha : releaseAlpha;
            smoothedSpectrum[i] = current + (target - current) * alpha;
        }

        savedTime = source.time;
        spectrumDataKnob?.SetValue(smoothedSpectrum);
        sampleRateKnob?.SetValue((float)AudioSettings.outputSampleRate);
        timeKnob?.SetValue(source.time);
        return true;
    }
}
