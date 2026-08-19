using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using SecretFire.TextureSynth.Timeline;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TimelineChannel
{
    public int id;
    public string name = "Signal";
    public TimelineCurve curve = new TimelineCurve();
    public float yMin = 0f;
    public float yMax = 1f;
    public TimelineChannel() { }
}

[Serializable]
public class TimelineEventMarker
{
    public int id;
    public float time;
    public TimelineEventMarker() { }
}

/// <summary>
/// Sequencer node: a transport (play/pause/rewind over a duration) driving N signal channels
/// (input × editable curve → output) and one-frame bool event pulses placed on a timeline track.
/// Scroll over the timeline zooms, middle-drag pans, right edge drag-resizes the node.
/// </summary>
[Node(false, "Signal/Timeline")]
public class TimelineNode : TickingNode
{
    public override string GetID => "TimelineNode";
    public override string Title { get { return "Timeline"; } }
    public override Vector2 DefaultSize => new Vector2(nodeWidth, ComputeHeight());
    public override Vector2 MinSize => new Vector2(MinNodeWidth, 120);

    // ---- serialized state ----
    public float duration = 60f;
    public float playhead = 0f;
    public bool playing = false;
    public bool loop = false;
    public float nodeWidth = 560f;
    public TimelineViewState view = new TimelineViewState();
    public List<TimelineChannel> channels = new List<TimelineChannel>();
    public List<TimelineEventMarker> events = new List<TimelineEventMarker>();
    public int nextChannelId = 0;
    public int nextEventId = 0;

    // ---- layout constants ----
    // Node.contentOffset is internal to the framework and constant (0, 20): the body GUI's
    // y=0 sits 20 canvas units below rect.y. Knob sidePositions are measured from rect.position.
    const float HeaderOffsetY = 20f;
    const float GutterWidth = 118f;
    const float TransportHeight = 24f;
    const float ChannelRowHeight = 52f;
    const float RowSpacing = 4f;
    const float RulerHeight = 20f;
    const float EventTrackHeight = 20f;
    const float BottomPad = 10f;
    const float MinNodeWidth = 460f;   // transport buttons + time readout at minimum width
    const float RightLabelWidth = 56f; // reserved column labeling the right-edge output ports
    const float FollowMarginFrac = 0.10f; // playhead keeps this fraction of the view from the edge
    const float MaxDuration = 86400f;  // 24h; also bounds ruler/grid tick loops
    const float ResizeStripWidth = 9f;
    const float MarkerStackSpacing = 20f;  // knob hit size is 16 canvas units
    const float KeyHitRadius = 8f;
    const float TangentHandleLength = 32f;
    const float CurveSampleStep = 2f;      // px between curve polyline samples

    static readonly Color RowBg       = new Color(0.10f, 0.10f, 0.10f, 1f);
    static readonly Color TrackBg     = new Color(0.07f, 0.07f, 0.07f, 1f);
    static readonly Color GridColor   = new Color(1f, 1f, 1f, 0.09f);
    static readonly Color CurveColor  = new Color(0.40f, 0.85f, 1.00f, 1f);
    static readonly Color KeyColor    = new Color(1.00f, 0.95f, 0.40f, 1f);
    static readonly Color KeySelected = new Color(1.00f, 0.55f, 0.30f, 1f);
    static readonly Color PlayheadCol = new Color(1.00f, 0.30f, 0.25f, 0.9f);
    static readonly Color MarkerColor = new Color(0.75f, 0.45f, 1.00f, 1f);
    static readonly Color EdgeGrip    = new Color(1f, 1f, 1f, 0.18f);

    // ---- runtime state ----
    enum DragKind { None, Playhead, Key, TangentIn, TangentOut, EventMarker, Pan, Resize }
    [NonSerialized] DragKind drag = DragKind.None;
    [NonSerialized] int dragChannel = -1;
    [NonSerialized] int dragKey = -1;
    [NonSerialized] int dragEvent = -1;
    [NonSerialized] Vector2 dragStartMouse;
    [NonSerialized] float dragStartWidth;
    [NonSerialized] int selChannel = -1;
    [NonSerialized] int selKey = -1;
    [NonSerialized] string activeFieldName = null;  // buffered text-field editing (one at a time)
    [NonSerialized] string activeFieldText = null;
    [NonSerialized] int lastCalcFrame = -1;
    [NonSerialized] bool fireEventsAtPlayhead = false;  // set when playback starts, so a cue at exactly the playhead fires
    [NonSerialized] float dragStartEventTime;           // marker drags move by time delta (stacked markers have no true pixel)
    [NonSerialized] float markersViewStart = float.NaN; // marker-pixel cache validity
    [NonSerialized] float markersViewEnd = float.NaN;
    [NonSerialized] float markersWidth = float.NaN;
    [NonSerialized] bool markersDirty = true;

    // ---- bound audio track (optional): transport-synced playback, waveform underlay,
    // ---- and AudioTrack-compatible spectrum outputs so one player serves both uses
    public string audioClipName = "";
    public bool audioBound = false;
    public float audioVolume = 1f;
    public float audioAttackTau = 0.04f;
    public float audioReleaseTau = 0.25f;

    const int SpectrumSize = 2048;
    const float AudioDriftThreshold = 0.15f; // seconds of drift before a corrective seek
    const int PeakBlockSize = 256;           // samples per precomputed waveform peak block
    const int WaveformTexHeight = 64;
    static readonly Color WaveformColor = new Color(0.42f, 0.60f, 0.80f, 0.38f);

    [NonSerialized] AudioSource audioSource;
    [NonSerialized] float[] peakMin;   // per-block min/max over the whole clip (mono mix)
    [NonSerialized] float[] peakMax;
    [NonSerialized] float[] rawSpectrum;
    [NonSerialized] float[] smoothedSpectrum;
    [NonSerialized] RenderTexture waveformTex;
    [NonSerialized] bool peaksPending = false;       // clip data still loading async
    [NonSerialized] float waveViewStart = float.NaN; // waveform cache validity
    [NonSerialized] float waveViewEnd = float.NaN;
    [NonSerialized] int waveWidth = -1;
    [NonSerialized] bool audioPickerOpen = false;
    [NonSerialized] Rect audioPickerRect;

    static ComputeShader waveShader;
    static int waveKernel;

    static GUIStyle _portLabelStyle;
    static GUIStyle PortLabelStyle
    {
        get
        {
            if (_portLabelStyle == null)
            {
                _portLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.55f) },
                };
            }
            return _portLabelStyle;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWaveShader()
    {
        waveShader = null;
        waveKernel = 0;
        _portLabelStyle = null;
    }

    // Node-local GUI rects, deterministic from nodeWidth/channel count (valid for every event type)
    [NonSerialized] Rect[] channelRects = new Rect[0];
    [NonSerialized] Rect curveAreaRect;      // shared x-range of curve rows / ruler / event track
    [NonSerialized] Rect rulerRect;
    [NonSerialized] Rect eventTrackRect;
    [NonSerialized] Rect timelineRegionRect; // rows + ruler + track: scroll-zoom + pan region
    [NonSerialized] Rect resizeStripRect;

    [NonSerialized] Dictionary<string, ValueConnectionKnob> knobsByName;
    [NonSerialized] float[] markerPixels = new float[0];
    [NonSerialized] List<float> eventTimesScratch = new List<float>();
    [NonSerialized] List<int> firedScratch = new List<int>();
    [NonSerialized] HashSet<int> firedIds = new HashSet<int>();
    [NonSerialized] Vector2[] curveSampleBuf = null;

    // ---------------------------------------------------------------- init / ports

    public override void DoInit()
    {
        if (channels == null) channels = new List<TimelineChannel>();
        if (events == null) events = new List<TimelineEventMarker>();
        if (view == null) view = new TimelineViewState();
        foreach (var ch in channels)
        {
            if (ch.curve == null) ch.curve = TimelineCurve.DefaultFlat(duration);
            ch.curve.EnsureValid();
        }
        if (view.Span <= 0f) view.Reset(duration);
        view.ClampTo(EffectiveViewMax());
        playhead = Mathf.Clamp(playhead, 0f, duration);
        if (audioBound && Application.isPlaying)
        {
            BindAudio(); // calls EnsurePorts itself (or unbinds on failure)
        }
        EnsurePorts();
        ComputeRects();
    }

    string InKnobName(TimelineChannel ch) => $"ch{ch.id} in";
    string OutKnobName(TimelineChannel ch) => $"ch{ch.id} out";
    string EventKnobName(TimelineEventMarker ev) => $"ev{ev.id}";

    ValueConnectionKnob Knob(string name)
    {
        if (knobsByName == null) return null;
        knobsByName.TryGetValue(name, out var knob);
        return knob;
    }

    // NodeEditorSaveManager.CreateWorkingCopy clones this node (firing Awake -> DoInit -> EnsurePorts)
    // BEFORE swapping dynamicConnectionPorts entries over to the cloned ports, so a cache built during
    // that Awake holds the source canvas's ports. All entries go stale together — checking one suffices.
    bool KnobCacheStale()
    {
        if (knobsByName == null) return true;
        int expected = channels.Count * 2 + events.Count + 4 + (audioBound ? 2 : 0); // time + 3 transport ins (+spectrum/sampleRate)
        if (knobsByName.Count != expected) return true;
        foreach (var knob in knobsByName.Values)
            return knob == null || !dynamicConnectionPorts.Contains(knob);
        return false;
    }

    void EnsurePorts()
    {
        if (knobsByName == null) knobsByName = new Dictionary<string, ValueConnectionKnob>();
        knobsByName.Clear();
        var required = new Dictionary<string, ValueConnectionKnobAttribute>();
        required["time"] = new ValueConnectionKnobAttribute("time", Direction.Out, typeof(float), NodeSide.Right);
        // Transport event pulses (KeySignal pressed / MIDINote etc.) on the top edge,
        // aligned above their matching buttons
        required["playPause"] = new ValueConnectionKnobAttribute("playPause", Direction.In, typeof(bool), NodeSide.Top);
        required["restart"] = new ValueConnectionKnobAttribute("restart", Direction.In, typeof(bool), NodeSide.Top);
        required["back5s"] = new ValueConnectionKnobAttribute("back5s", Direction.In, typeof(bool), NodeSide.Top);
        if (audioBound)
        {
            required["spectrum"] = new ValueConnectionKnobAttribute("spectrum", Direction.Out, typeof(float[]), NodeSide.Right);
            required["sampleRate"] = new ValueConnectionKnobAttribute("sampleRate", Direction.Out, typeof(float), NodeSide.Right);
        }
        foreach (var ch in channels)
        {
            required[InKnobName(ch)] = new ValueConnectionKnobAttribute(InKnobName(ch), Direction.In, typeof(float), NodeSide.Left);
            required[OutKnobName(ch)] = new ValueConnectionKnobAttribute(OutKnobName(ch), Direction.Out, typeof(float), NodeSide.Right);
        }
        foreach (var ev in events)
        {
            required[EventKnobName(ev)] = new ValueConnectionKnobAttribute(EventKnobName(ev), Direction.Out, typeof(bool), NodeSide.Bottom);
        }
        // Delete orphaned ports (channel/event removed), keep existing, note what's present
        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (required.ContainsKey(port.name))
            {
                knobsByName[port.name] = port;
            }
            else
            {
                port.ClearConnections();
                DeleteConnectionPort(port);
            }
        }
        // Create missing
        foreach (var kv in required)
        {
            if (!knobsByName.ContainsKey(kv.Key))
            {
                knobsByName[kv.Key] = CreateValueConnectionKnob(kv.Value);
            }
        }
    }

    // ---------------------------------------------------------------- layout

    float AudioPickerHeight()
    {
        if (!audioPickerOpen) return 0f;
        if (!Application.isPlaying) return 26f;
        if (audioBound) return 46f; // clip label + volume slider + unbind
        int clipCount = AudioTrackManager.Instance != null ? AudioTrackManager.Instance.ClipNames.Length : 0;
        return 28f + Mathf.Max(clipCount, 1) * 22f;
    }

    float ComputeHeight()
    {
        int chCount = channels != null ? channels.Count : 0;
        return HeaderOffsetY + TransportHeight + RowSpacing + AudioPickerHeight()
             + chCount * (ChannelRowHeight + RowSpacing)
             + RulerHeight + 2f + EventTrackHeight + BottomPad;
    }

    void ComputeRects()
    {
        int chCount = channels.Count;
        if (channelRects.Length != chCount) channelRects = new Rect[chCount];
        float width = Mathf.Max(nodeWidth, MinNodeWidth);
        float curveX = GutterWidth;
        float curveW = Mathf.Max(width - GutterWidth - RightLabelWidth, 20f);
        float pickerH = AudioPickerHeight();
        audioPickerRect = new Rect(6f, TransportHeight + 2f, width - 24f, pickerH);
        float y = TransportHeight + RowSpacing + pickerH;
        for (int i = 0; i < chCount; i++)
        {
            channelRects[i] = new Rect(curveX, y, curveW, ChannelRowHeight);
            y += ChannelRowHeight + RowSpacing;
        }
        rulerRect = new Rect(curveX, y, curveW, RulerHeight);
        y += RulerHeight + 2f;
        eventTrackRect = new Rect(curveX, y, curveW, EventTrackHeight);
        float regionTop = TransportHeight + RowSpacing;
        curveAreaRect = new Rect(curveX, regionTop, curveW, (y + EventTrackHeight) - regionTop);
        timelineRegionRect = curveAreaRect;
        resizeStripRect = new Rect(width - ResizeStripWidth, 0f, ResizeStripWidth, ComputeHeight() - HeaderOffsetY);
        UpdateMarkerPixels();
    }

    void UpdateMarkerPixels()
    {
        // MarkerPositions allocates; recompute only when the view, width, or events changed
        bool viewChanged = view.viewStart != markersViewStart || view.viewEnd != markersViewEnd
                        || eventTrackRect.width != markersWidth;
        if (!markersDirty && !viewChanged && markerPixels.Length >= events.Count) return;
        if (markerPixels.Length < events.Count) markerPixels = new float[events.Count + 8];
        eventTimesScratch.Clear();
        for (int i = 0; i < events.Count; i++) eventTimesScratch.Add(events[i].time);
        TimelineViewState.MarkerPositions(eventTimesScratch, view.viewStart, view.viewEnd,
            eventTrackRect.width, MarkerStackSpacing, markerPixels);
        markersViewStart = view.viewStart;
        markersViewEnd = view.viewEnd;
        markersWidth = eventTrackRect.width;
        markersDirty = false;
    }

    // While playing, the view scrolls to keep the playhead in frame with a margin off the
    // edge. Backwards jumps (loop wrap, back-to-start) snap the window to the left margin.
    void FollowPlayhead()
    {
        float margin = view.Span * FollowMarginFrac;
        float shift = 0f;
        if (playhead > view.viewEnd - margin)
        {
            shift = playhead - (view.viewEnd - margin);
        }
        else if (playhead < view.viewStart)
        {
            shift = playhead - (view.viewStart + margin);
        }
        if (shift != 0f)
        {
            view.viewStart += shift;
            view.viewEnd += shift;
            view.ClampTo(EffectiveViewMax());
        }
    }

    // ---------------------------------------------------------------- audio track

    void BindAudio()
    {
        audioSource = AudioTrackManager.Instance.CreateSource(this, audioClipName);
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogError($"Timeline: failed to bind audio '{audioClipName}', unbinding.");
            UnbindAudio();
            return;
        }
        audioSource.volume = audioVolume;
        if (rawSpectrum == null) rawSpectrum = new float[SpectrumSize];
        if (smoothedSpectrum == null) smoothedSpectrum = new float[SpectrumSize];
        ExtractPeaks(audioSource.clip);
        // An audio timeline is 1:1 with its clip: re-anchor the duration WITHOUT rescaling
        // existing keys/events (this is a re-anchoring, not a stretch). On reload the saved
        // duration already matches, so nothing moves.
        float len = audioSource.clip.length;
        if (Mathf.Abs(duration - len) > 0.01f)
        {
            Debug.Log($"Timeline: duration set to bound clip length {len:0.##}s (was {duration:0.##}s).");
            duration = Mathf.Min(len, MaxDuration);
            playhead = Mathf.Clamp(playhead, 0f, duration);
            view.Reset(duration);
            markersDirty = true;
        }
        waveViewStart = float.NaN; // force waveform rebuild
        EnsurePorts();
    }

    void UnbindAudio()
    {
        audioBound = false;
        audioClipName = "";
        if (Application.isPlaying && AudioTrackManager.Instance != null)
        {
            AudioTrackManager.Instance.ReleaseSource(this);
        }
        audioSource = null;
        peakMin = null;
        peakMax = null;
        if (waveformTex != null)
        {
            waveformTex.Release();
            waveformTex = null;
        }
        EnsurePorts(); // drops the spectrum/sampleRate ports
    }

    // One-shot scan at bind: per-block min/max of the mono mix, so waveform columns for any
    // zoom aggregate a few blocks instead of rescanning millions of raw samples.
    void ExtractPeaks(AudioClip clip)
    {
        peakMin = null;
        peakMax = null;
        peaksPending = false;
        if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
        {
            Debug.LogWarning($"Timeline: waveform display needs a Decompress-On-Load clip " +
                             $"('{clip.name}' is {clip.loadType}); playback still works.");
            return;
        }
        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            // 'Preload Audio Data' unchecked: sample data loads asynchronously on demand.
            // Kick the load and let SyncAudioPlayback retry once it's ready.
            clip.LoadAudioData();
            peaksPending = true;
            return;
        }
        int channelCount = clip.channels;
        var data = new float[clip.samples * channelCount];
        if (!clip.GetData(data, 0)) return;
        int blocks = Mathf.CeilToInt((float)clip.samples / PeakBlockSize);
        peakMin = new float[blocks];
        peakMax = new float[blocks];
        for (int b = 0; b < blocks; b++)
        {
            float lo = 0f, hi = 0f;
            int s0 = b * PeakBlockSize;
            int s1 = Mathf.Min(s0 + PeakBlockSize, clip.samples);
            for (int s = s0; s < s1; s++)
            {
                float mono = 0f;
                int baseIdx = s * channelCount;
                for (int c = 0; c < channelCount; c++) mono += data[baseIdx + c];
                mono /= channelCount;
                if (mono < lo) lo = mono;
                if (mono > hi) hi = mono;
            }
            peakMin[b] = lo;
            peakMax[b] = hi;
        }
    }

    // Keeps the audio locked to the transport: play/pause follows `playing`, position
    // drift-corrects to the playhead (scrubs, loops and jumps all just look like drift).
    void SyncAudioPlayback()
    {
        if (audioSource == null || audioSource.clip == null) return;
        if (peaksPending)
        {
            var loadState = audioSource.clip.loadState;
            if (loadState == AudioDataLoadState.Loaded)
            {
                ExtractPeaks(audioSource.clip);
                waveViewStart = float.NaN; // force waveform rebuild with real data
            }
            else if (loadState == AudioDataLoadState.Failed)
            {
                peaksPending = false;
                Debug.LogWarning($"Timeline: audio data failed to load for '{audioSource.clip.name}'; waveform unavailable.");
            }
        }
        audioSource.volume = audioVolume;
        float len = audioSource.clip.length;
        bool shouldPlay = playing && playhead < len;
        if (shouldPlay && !audioSource.isPlaying)
        {
            audioSource.time = Mathf.Min(playhead, Mathf.Max(len - 0.05f, 0f));
            audioSource.Play();
        }
        else if (!shouldPlay && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
        if (audioSource.isPlaying && playhead < len &&
            Mathf.Abs(audioSource.time - playhead) > AudioDriftThreshold)
        {
            audioSource.time = playhead;
        }

        if (rawSpectrum != null && smoothedSpectrum != null)
        {
            // dB-normalized to [0,1] over -60..0 dBFS + per-bin attack/release, matching
            // AudioTrackNode/SystemAudioSpectrum so downstream chains are interchangeable
            audioSource.GetSpectrumData(rawSpectrum, 0, FFTWindow.BlackmanHarris);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float attackAlpha = audioAttackTau > 1e-4f ? 1f - Mathf.Exp(-dt / audioAttackTau) : 1f;
            float releaseAlpha = audioReleaseTau > 1e-4f ? 1f - Mathf.Exp(-dt / audioReleaseTau) : 1f;
            for (int i = 0; i < SpectrumSize; i++)
            {
                float db = 20f * Mathf.Log10(rawSpectrum[i] / 0.7071f + 1.5849e-13f);
                // Clamped so silence floors at 0 instead of ≈ -3.3 (unclamped dB mapping)
                float target = Mathf.Clamp01((db + 60f) / 60f);
                float current = smoothedSpectrum[i];
                smoothedSpectrum[i] = current + (target - current) * (target > current ? attackAlpha : releaseAlpha);
            }
        }

        UpdateWaveformTexture();
    }

    // Renders per-column (min,max) peaks for the visible window into waveformTex.
    // Only recomputes when the view window or width changed.
    void UpdateWaveformTexture()
    {
        if (peakMin == null || audioSource == null || audioSource.clip == null) return;
        int width = Mathf.Max(Mathf.RoundToInt(curveAreaRect.width), 8);
        if (waveformTex != null && width == waveWidth &&
            view.viewStart == waveViewStart && view.viewEnd == waveViewEnd) return;

        if (waveShader == null)
        {
            waveShader = Resources.Load<ComputeShader>("NodeShaders/Waveform");
            if (waveShader == null) return;
            waveKernel = waveShader.FindKernel("WaveformShader");
        }
        if (waveformTex == null || waveformTex.width != width)
        {
            if (waveformTex != null) waveformTex.Release();
            waveformTex = new RenderTexture(width, WaveformTexHeight, 0)
            {
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave,
            };
            waveformTex.Create();
        }

        var clip = audioSource.clip;
        float blockDuration = (float)PeakBlockSize / clip.frequency;
        var columns = new Vector2[width];
        for (int x = 0; x < width; x++)
        {
            float t0 = view.PixelToTime(x, width);
            float t1 = view.PixelToTime(x + 1, width);
            int b0 = Mathf.FloorToInt(t0 / blockDuration);
            int b1 = Mathf.FloorToInt(t1 / blockDuration);
            float lo = 0f, hi = 0f;
            if (b1 >= 0 && b0 < peakMin.Length && t0 <= clip.length)
            {
                b0 = Mathf.Max(b0, 0);
                b1 = Mathf.Min(b1, peakMin.Length - 1);
                for (int b = b0; b <= b1; b++)
                {
                    if (peakMin[b] < lo) lo = peakMin[b];
                    if (peakMax[b] > hi) hi = peakMax[b];
                }
            }
            columns[x] = new Vector2(lo, hi);
        }

        var buffer = new ComputeBuffer(width, 8);
        buffer.SetData(columns);
        waveShader.SetBuffer(waveKernel, "columnRange", buffer);
        waveShader.SetVector("bgColor", Color.clear);
        waveShader.SetVector("activeColor", WaveformColor);
        waveShader.SetInt("outWidth", width);
        waveShader.SetInt("outHeight", WaveformTexHeight);
        waveShader.SetTexture(waveKernel, "waveform", waveformTex);
        waveShader.Dispatch(waveKernel, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(WaveformTexHeight / 16f), 1);
        buffer.Release();

        waveWidth = width;
        waveViewStart = view.viewStart;
        waveViewEnd = view.viewEnd;
    }

    float TimeToX(float t) => curveAreaRect.x + view.TimeToPixel(t, curveAreaRect.width);
    float XToTime(float x) => view.PixelToTime(x - curveAreaRect.x, curveAreaRect.width);

    float ValueToY(TimelineChannel ch, Rect row, float v)
    {
        float range = Mathf.Max(ch.yMax - ch.yMin, 1e-4f);
        return row.yMax - Mathf.Clamp01((v - ch.yMin) / range) * row.height;
    }

    float YToValue(TimelineChannel ch, Rect row, float y)
    {
        float range = Mathf.Max(ch.yMax - ch.yMin, 1e-4f);
        return ch.yMin + Mathf.Clamp01((row.yMax - y) / row.height) * range;
    }

    // ---------------------------------------------------------------- calc

    public override bool DoCalc()
    {
        if (KnobCacheStale()) EnsurePorts();
        // OnNodeChange (from GUI.changed) can re-run Calculate in the same frame; the transport
        // must only advance once per frame, and fired pulses must survive the re-run.
        if (Time.frameCount != lastCalcFrame)
        {
            lastCalcFrame = Time.frameCount;
            firedIds.Clear();
            // Bound transport pulses, mirroring the buttons (one-tick bools expected)
            if (KnobPulse("playPause"))
            {
                playing = !playing;
                if (playing) fireEventsAtPlayhead = true;
            }
            if (KnobPulse("restart"))
            {
                playhead = 0f;
                playing = false;
            }
            if (KnobPulse("back5s"))
            {
                playhead = Mathf.Max(playhead - 5f, 0f);
            }
            if (playing && duration > 0f)
            {
                eventTimesScratch.Clear();
                for (int i = 0; i < events.Count; i++) eventTimesScratch.Add(events[i].time);
                firedScratch.Clear();
                if (fireEventsAtPlayhead)
                {
                    // playback just started: a cue sitting exactly at the playhead (e.g. t=0) fires
                    TimelineEventLogic.FiredAt(playhead, eventTimesScratch, firedScratch, 1e-4f);
                    fireEventsAtPlayhead = false;
                }
                playhead = TimelineEventLogic.Advance(playhead, Time.deltaTime, duration, loop,
                    eventTimesScratch, firedScratch, out bool wrapped, out bool reachedEnd);
                for (int i = 0; i < firedScratch.Count; i++) firedIds.Add(events[firedScratch[i]].id);
                if (reachedEnd) playing = false;
                FollowPlayhead();
            }
            if (audioBound)
            {
                SyncAudioPlayback(); // once per frame: transport coupling, spectrum, waveform
            }
        }
        foreach (var ch in channels)
        {
            var outKnob = Knob(OutKnobName(ch));
            if (outKnob == null) continue;
            var inKnob = Knob(InKnobName(ch));
            float input = (inKnob != null && inKnob.connected()) ? inKnob.GetValue<float>() : 1f;
            // Clamped to the channel range: hermite overshoot between keys can exceed
            // [yMin, yMax], and the editor already RENDERS the curve clamped — the
            // emitted value should match what's drawn
            float curveValue = Mathf.Clamp(ch.curve.Evaluate(playhead), ch.yMin, ch.yMax);
            outKnob.SetValue<float>(input * curveValue);
        }
        foreach (var ev in events)
        {
            Knob(EventKnobName(ev))?.SetValue<bool>(firedIds.Contains(ev.id));
        }
        Knob("time")?.SetValue<float>(playhead);
        if (audioBound && smoothedSpectrum != null)
        {
            Knob("spectrum")?.SetValue(smoothedSpectrum);
            Knob("sampleRate")?.SetValue((float)AudioSettings.outputSampleRate);
        }
        // Keep rects/knob positions current even when the canvas GUI isn't being drawn,
        // so connection wires track zoom/pan without a one-repaint lag.
        ComputeRects();
        PositionKnobs();
        return true;
    }

    bool KnobPulse(string name)
    {
        var knob = Knob(name);
        return knob != null && knob.connected() && knob.GetValue<bool>();
    }

    void PositionKnobs()
    {
        // Transport inputs sit above their matching buttons (▶ ⏮ ↶5)
        Knob("playPause")?.SetPosition(21f);
        Knob("restart")?.SetPosition(55f);
        Knob("back5s")?.SetPosition(89f);
        Knob("time")?.SetPosition(HeaderOffsetY + TransportHeight * 0.5f);
        if (audioBound)
        {
            Knob("spectrum")?.SetPosition(rulerRect.center.y + HeaderOffsetY);
            Knob("sampleRate")?.SetPosition(eventTrackRect.center.y + HeaderOffsetY);
        }
        for (int i = 0; i < channels.Count && i < channelRects.Length; i++)
        {
            float y = channelRects[i].center.y + HeaderOffsetY;
            Knob(InKnobName(channels[i]))?.SetPosition(y);
            Knob(OutKnobName(channels[i]))?.SetPosition(y);
        }
        for (int i = 0; i < events.Count && i < markerPixels.Length; i++)
        {
            Knob(EventKnobName(events[i]))?.SetPosition(eventTrackRect.x + markerPixels[i]);
        }
    }

    // ---------------------------------------------------------------- GUI

    public override void NodeGUI()
    {
        // RTNodeEditor discards the runtime canvas if NodeGUI throws — never let anything escape.
        try
        {
            // Reserve the body as one layout rect: Node.DrawNode reads GUILayoutUtility.GetLastRect()
            // after NodeGUI, which errors if the GUI emitted zero layout entries.
            GUILayoutUtility.GetRect(nodeWidth - 12f, ComputeHeight() - HeaderOffsetY - 6f);
            ComputeRects();
            HandleInput();
            DrawTransport();
            DrawAudioPicker();
            DrawChannels();
            DrawRuler();
            DrawEventTrack();
            DrawPlayhead();
            DrawPortLabels();
            DrawResizeGrip();
            PositionKnobs();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void DrawTransport()
    {
        float x = 6f;
        Rect Next(float w) { var r = new Rect(x, 2f, w, TransportHeight - 4f); x += w + 4f; return r; }

        if (GUI.Button(Next(30f), playing ? "❚❚" : "▶"))
        {
            playing = !playing;
            if (playing) fireEventsAtPlayhead = true;
        }
        if (GUI.Button(Next(30f), "⏮"))
        {
            playhead = 0f;
            playing = false;
        }
        if (GUI.Button(Next(30f), "↶5"))
        {
            playhead = Mathf.Max(playhead - 5f, 0f);
        }
        loop = GUI.Toggle(Next(44f), loop, "Loop", GUI.skin.button);
        if (GUI.Button(Next(44f), "Zoom⟲")) view.Reset(duration);
        if (GUI.Button(Next(56f), "+ Signal")) AddChannel();
        if (GUI.Toggle(Next(26f), audioPickerOpen, "♪", GUI.skin.button) != audioPickerOpen)
        {
            audioPickerOpen = !audioPickerOpen;
        }

        float newDur = MiniFloatField(Next(52f), "dur", duration);
        if (newDur != duration && newDur >= 0.1f)
        {
            newDur = Mathf.Min(newDur, MaxDuration);
            // Duration changes rescale the whole timeline proportionally: keys, events,
            // playhead, and view all keep their relative positions (10s->5s moves t=8 to t=4)
            float factor = newDur / duration;
            foreach (var ch in channels) ch.curve.ScaleTimes(factor);
            foreach (var ev in events) ev.time *= factor;
            playhead = Mathf.Clamp(playhead * factor, 0f, newDur);
            view.viewStart *= factor;
            view.viewEnd *= factor;
            duration = newDur;
            view.ClampTo(EffectiveViewMax());
            markersDirty = true;
        }

        GUI.Label(new Rect(x, 2f, 140f, TransportHeight - 4f), $"{FormatTime(playhead)} / {FormatTime(duration)}");
    }

    // Text field that edits a float via a buffer: shows the typed text while focused,
    // commits on Enter or focus loss, and never fights the user's mid-edit input.
    float MiniFloatField(Rect r, string fieldKey, float value)
    {
        string controlName = $"tl{GetInstanceID()}_{fieldKey}";
        GUI.SetNextControlName(controlName);
        bool editing = activeFieldName == controlName;
        string shown = editing ? activeFieldText : value.ToString("0.###");
        string typed = GUI.TextField(r, shown);
        bool focused = GUI.GetNameOfFocusedControl() == controlName;
        if (focused)
        {
            if (!editing)
            {
                activeFieldName = controlName;
                activeFieldText = shown;
            }
            if (typed != shown) activeFieldText = typed;
            bool enter = Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (enter && TryParseFinite(activeFieldText, out float entered))
            {
                activeFieldName = null;
                return entered;
            }
        }
        else if (editing)
        {
            activeFieldName = null;
            if (TryParseFinite(activeFieldText, out float committed))
            {
                return committed;
            }
        }
        return value;
    }

    static bool TryParseFinite(string text, out float value)
    {
        return float.TryParse(text, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static string FormatTime(float t)
    {
        // round to tenths first so 59.96s displays as 1:00.0, not 0:60.0
        long tenths = (long)Mathf.Round(Mathf.Max(t, 0f) * 10f);
        long mins = tenths / 600;
        return $"{mins}:{(tenths % 600) / 10f:00.0}";
    }

    // Audio panel under the transport (♪ button toggles): clip picker when unbound,
    // clip info + volume + unbind when bound. Fixed rects — no GUILayout entries.
    void DrawAudioPicker()
    {
        if (!audioPickerOpen) return;
        var r = audioPickerRect;
        FillRect(r, new Color(0.16f, 0.16f, 0.17f, 1f));
        if (!Application.isPlaying)
        {
            GUI.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, 18f), "Enter play mode to bind audio");
            return;
        }
        if (audioBound)
        {
            GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 120f, 16f), $"♪ {audioClipName}");
            if (GUI.Button(new Rect(r.xMax - 96f, r.y + 2f, 92f, 18f), "Unbind audio"))
            {
                UnbindAudio();
                return;
            }
            GUI.Label(new Rect(r.x + 6f, r.y + 24f, 28f, 16f), "Vol");
            audioVolume = GUI.HorizontalSlider(new Rect(r.x + 38f, r.y + 28f, r.width - 50f, 12f), audioVolume, 0f, 1f);
        }
        else
        {
            GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 12f, 16f), "Bind audio track:");
            var names = AudioTrackManager.Instance.ClipNames;
            if (names.Length == 0)
            {
                GUI.Label(new Rect(r.x + 6f, r.y + 26f, r.width - 12f, 18f), "No clips found (Resources/AudioTracks)");
                return;
            }
            for (int i = 0; i < names.Length; i++)
            {
                if (GUI.Button(new Rect(r.x + 6f, r.y + 26f + i * 22f, r.width - 12f, 20f), names[i]))
                {
                    audioClipName = names[i];
                    audioBound = true;
                    audioPickerOpen = false;
                    BindAudio();
                    return;
                }
            }
        }
    }

    // Keys/events beyond a shrunk duration stay editable: the view may extend to reach them
    float EffectiveViewMax()
    {
        float max = duration;
        for (int i = 0; i < events.Count; i++) max = Mathf.Max(max, events[i].time);
        for (int c = 0; c < channels.Count; c++)
        {
            var curve = channels[c].curve;
            if (curve.KeyCount > 0) max = Mathf.Max(max, curve.GetKey(curve.KeyCount - 1).time);
        }
        return max;
    }

    void DrawChannels()
    {
        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            var row = channelRects[i];

            // gutter widgets
            float gy = row.y + 2f;
            ch.name = GUI.TextField(new Rect(4f, gy, GutterWidth - 30f, 18f), ch.name ?? "");
            if (GUI.Button(new Rect(GutterWidth - 24f, gy, 18f, 18f), "×"))
            {
                RemoveChannel(i);
                return; // indices shifted; draw next frame
            }
            // y-range: plain min/max fields (default 0 and 1)
            ch.yMin = MiniFloatField(new Rect(4f, gy + 22f, 46f, 16f), $"ch{ch.id}min", ch.yMin);
            GUI.Label(new Rect(52f, gy + 22f, 12f, 16f), "..");
            ch.yMax = MiniFloatField(new Rect(64f, gy + 22f, 46f, 16f), $"ch{ch.id}max", ch.yMax);
            if (ch.yMax <= ch.yMin) ch.yMax = ch.yMin + 0.001f;

            // row background + waveform underlay + grid + curve (GL calls draw on Repaint only)
            FillRect(row, RowBg);
            if (audioBound && waveformTex != null)
            {
                GUI.DrawTexture(row, waveformTex, ScaleMode.StretchToFill);
            }
            DrawGrid(row);
            DrawCurve(i, ch, row);
        }
    }

    void DrawGrid(Rect row)
    {
        float step = TimelineViewState.SelectTickStep(view.Span, row.width, 60f);
        float t0 = Mathf.Ceil(view.viewStart / step) * step;
        int guard = 0;
        for (float t = t0; t <= view.viewEnd + 1e-4f && guard++ < 512; t += step)
        {
            float px = TimeToX(t);
            FillRect(new Rect(px, row.y, 1f, row.height), GridColor);
        }
    }

    void DrawCurve(int chIdx, TimelineChannel ch, Rect row)
    {
        if (Event.current.type != EventType.Repaint) return;
        bool selected = chIdx == selChannel;

        int samples = Mathf.Max(2, (int)(row.width / CurveSampleStep) + 1);
        if (curveSampleBuf == null || curveSampleBuf.Length != samples)
            curveSampleBuf = new Vector2[samples];
        for (int s = 0; s < samples; s++)
        {
            float px = row.x + (row.width * s) / (samples - 1);
            float t = XToTime(px);
            curveSampleBuf[s] = new Vector2(px, ValueToY(ch, row, ch.curve.Evaluate(t)));
        }
        RTEditorGUI.DrawPolygonLine(curveSampleBuf, CurveColor, Texture2D.whiteTexture, 2f);

        // keys + selected key's tangent handles
        for (int k = 0; k < ch.curve.KeyCount; k++)
        {
            var key = ch.curve.GetKey(k);
            if (key.time < view.viewStart || key.time > view.viewEnd) continue;
            var p = new Vector2(TimeToX(key.time), ValueToY(ch, row, key.value));
            bool isSel = selected && k == selKey;
            if (isSel)
            {
                DrawTangentHandle(ch, row, key, p, true);
                DrawTangentHandle(ch, row, key, p, false);
            }
            FillRect(new Rect(p.x - 3f, p.y - 3f, 7f, 7f), isSel ? KeySelected : KeyColor);
        }

        // precise-positioning readout for the dragged or hovered key
        int annotate = -1;
        if ((drag == DragKind.Key || drag == DragKind.TangentIn || drag == DragKind.TangentOut) && dragChannel == chIdx)
            annotate = dragKey;
        else if (drag == DragKind.None && row.Contains(Event.current.mousePosition))
            annotate = KeyAt(chIdx, Event.current.mousePosition);
        var aKey = ch.curve.GetKey(annotate);
        if (aKey != null)
        {
            var p = new Vector2(TimeToX(aKey.time), ValueToY(ch, row, aKey.value));
            string label = $"{aKey.time:0.00}s, {aKey.value:0.###}";
            Vector2 size = GUI.skin.label.CalcSize(new GUIContent(label));
            var lr = new Rect(p.x + 8f, p.y - size.y - 2f, size.x + 6f, size.y);
            if (lr.xMax > row.xMax) lr.x = p.x - lr.width - 8f;
            if (lr.y < row.y) lr.y = p.y + 6f;
            FillRect(lr, new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(lr.x + 3f, lr.y, size.x, size.y), label);
        }
    }

    void DrawTangentHandle(TimelineChannel ch, Rect row, CurveKey key, Vector2 keyPx, bool inHandle)
    {
        Vector2 hp = TangentHandlePos(ch, row, key, keyPx, inHandle);
        RTEditorGUI.DrawLine(keyPx, hp, new Color(1f, 1f, 1f, 0.5f), Texture2D.whiteTexture, 1f);
        // broken handles read orange so sharp corners are visible at a glance
        FillRect(new Rect(hp.x - 2f, hp.y - 2f, 5f, 5f), key.broken ? KeySelected : Color.white);
    }

    Vector2 TangentHandlePos(TimelineChannel ch, Rect row, CurveKey key, Vector2 keyPx, bool inHandle)
    {
        float pxPerSec = row.width / Mathf.Max(view.Span, 1e-4f);
        float pxPerVal = row.height / Mathf.Max(ch.yMax - ch.yMin, 1e-4f);
        float slope = inHandle ? key.inTangent : key.outTangent;
        var dir = new Vector2(1f, -slope * pxPerVal / pxPerSec).normalized * TangentHandleLength;
        return inHandle ? keyPx - dir : keyPx + dir;
    }

    void DrawRuler()
    {
        FillRect(rulerRect, TrackBg);
        float step = TimelineViewState.SelectTickStep(view.Span, rulerRect.width, 60f);
        float minor = step / 5f;
        float t0 = Mathf.Ceil(view.viewStart / minor) * minor;
        int guard = 0;
        for (float t = t0; t <= view.viewEnd + 1e-4f && guard++ < 2048; t += minor)
        {
            float px = TimeToX(t);
            bool major = Mathf.Abs(t / step - Mathf.Round(t / step)) < 1e-3f;
            FillRect(new Rect(px, rulerRect.y, 1f, major ? rulerRect.height : rulerRect.height * 0.4f),
                new Color(1f, 1f, 1f, major ? 0.5f : 0.25f));
            if (major)
                GUI.Label(new Rect(px + 2f, rulerRect.y + 2f, 48f, 16f), FormatTime(t));
        }
    }

    void DrawEventTrack()
    {
        FillRect(eventTrackRect, TrackBg);
        for (int i = 0; i < events.Count && i < markerPixels.Length; i++)
        {
            float px = eventTrackRect.x + markerPixels[i];
            bool inView = events[i].time >= view.viewStart && events[i].time <= view.viewEnd;
            var col = inView ? MarkerColor : new Color(MarkerColor.r, MarkerColor.g, MarkerColor.b, 0.5f);
            FillRect(new Rect(px - 3f, eventTrackRect.y + 3f, 7f, eventTrackRect.height - 6f), col);
        }
    }

    void DrawPlayhead()
    {
        if (playhead < view.viewStart || playhead > view.viewEnd) return;
        float px = TimeToX(playhead);
        FillRect(new Rect(px, curveAreaRect.y, 1f, curveAreaRect.height), PlayheadCol);
        FillRect(new Rect(px - 3f, rulerRect.y, 7f, 4f), PlayheadCol);
    }

    // Small right-aligned labels in the reserved right column identifying the output ports
    void DrawPortLabels()
    {
        float x = curveAreaRect.xMax + 4f;
        float w = Mathf.Max(nodeWidth, MinNodeWidth) - curveAreaRect.xMax - 16f;
        if (w < 20f) return;
        GUI.Label(new Rect(x, TransportHeight * 0.5f - 8f, w, 16f), "time", PortLabelStyle);
        for (int i = 0; i < channelRects.Length && i < channels.Count; i++)
        {
            GUI.Label(new Rect(x, channelRects[i].center.y - 8f, w, 16f), "out", PortLabelStyle);
        }
        if (audioBound)
        {
            GUI.Label(new Rect(x, rulerRect.center.y - 8f, w, 16f), "spectrum", PortLabelStyle);
            GUI.Label(new Rect(x, eventTrackRect.center.y - 8f, w, 16f), "rate", PortLabelStyle);
        }
    }

    void DrawResizeGrip()
    {
        for (int i = 0; i < 3; i++)
            FillRect(new Rect(resizeStripRect.x + 3f, resizeStripRect.center.y - 10f + i * 8f, 3f, 3f), EdgeGrip);
    }

    static void FillRect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // ---------------------------------------------------------------- input

    void HandleInput()
    {
        Event e = Event.current;
        if (e == null) return;
        // Only react when this node is the one under the pointer (nodes can overlap)
        if (e.type == EventType.MouseDown &&
            NodeEditor.curEditorState != null && NodeEditor.curEditorState.focusedNode != this)
            return;
        Vector2 m = e.mousePosition;

        // A MouseUp consumed by another handler (type == Used) or released outside the
        // window (type == Ignore) never matches the MouseUp case below; rawType still
        // says MouseUp. Without this, the drag pseudo-capture sticks across clicks.
        if (drag != DragKind.None && e.rawType == EventType.MouseUp && e.type != EventType.MouseUp)
        {
            drag = DragKind.None;
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                // right-click delete is handled by the pre-GUI HandleTimelineRightClick handler:
                // the framework's context-menu handler consumes button-1 events before NodeGUI runs
                if (e.button == 0) HandleLeftDown(m, e);
                else if (e.button == 2 && timelineRegionRect.Contains(m))
                {
                    drag = DragKind.Pan;
                    dragStartMouse = m;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (drag != DragKind.None)
                {
                    HandleDrag(m, e);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (drag != DragKind.None)
                {
                    drag = DragKind.None;
                    e.Use();
                }
                break;
        }
    }

    void HandleLeftDown(Vector2 m, Event e)
    {
        if (resizeStripRect.Contains(m))
        {
            drag = DragKind.Resize;
            dragStartMouse = m;
            dragStartWidth = nodeWidth;
            e.Use();
            return;
        }
        if (rulerRect.Contains(m))
        {
            drag = DragKind.Playhead;
            playhead = Mathf.Clamp(XToTime(m.x), 0f, duration);
            e.Use();
            return;
        }
        if (eventTrackRect.Contains(m))
        {
            int hit = EventMarkerAt(m.x);
            if (hit < 0)
            {
                var ev = new TimelineEventMarker { id = nextEventId++, time = Mathf.Clamp(XToTime(m.x), 0f, duration) };
                events.Add(ev);
                EnsurePorts();
                markersDirty = true;
                UpdateMarkerPixels();
                hit = events.Count - 1;
            }
            drag = DragKind.EventMarker;
            dragEvent = hit;
            dragStartMouse = m;
            dragStartEventTime = events[hit].time;
            e.Use();
            return;
        }
        // A selected key's tangent handles may extend past its row into a neighbor row —
        // test them before row hit-testing so they stay grabbable.
        if (selChannel >= 0 && selChannel < channels.Count && selKey >= 0 &&
            selChannel < channelRects.Length && selKey < channels[selChannel].curve.KeyCount)
        {
            if (TryStartTangentDrag(selChannel, m))
            {
                e.Use();
                return;
            }
        }
        int chIdx = ChannelAt(m);
        if (chIdx >= 0)
        {
            OnCurveLeftDown(chIdx, m, e);
            e.Use();
        }
    }

    bool TryStartTangentDrag(int chIdx, Vector2 m)
    {
        var ch = channels[chIdx];
        var row = channelRects[chIdx];
        var key = ch.curve.GetKey(selKey);
        if (key == null) return false;
        var keyPx = new Vector2(TimeToX(key.time), ValueToY(ch, row, key.value));
        if ((TangentHandlePos(ch, row, key, keyPx, true) - m).magnitude <= KeyHitRadius)
        {
            drag = DragKind.TangentIn;
            dragChannel = chIdx;
            dragKey = selKey;
            return true;
        }
        if ((TangentHandlePos(ch, row, key, keyPx, false) - m).magnitude <= KeyHitRadius)
        {
            drag = DragKind.TangentOut;
            dragChannel = chIdx;
            dragKey = selKey;
            return true;
        }
        return false;
    }

    void OnCurveLeftDown(int chIdx, Vector2 m, Event e)
    {
        var ch = channels[chIdx];
        var row = channelRects[chIdx];
        // (tangent handles were already tested in HandleLeftDown, across row bounds)
        int keyIdx = KeyAt(chIdx, m);
        if (keyIdx >= 0)
        {
            selChannel = chIdx;
            selKey = keyIdx;
            var hitKey = ch.curve.GetKey(keyIdx);
            if (e.alt && hitKey != null && hitKey.broken)
            {
                // alt-click on a broken key re-links its handles (no drag on this click)
                ch.curve.RejoinTangents(keyIdx);
                return;
            }
            drag = DragKind.Key;
            dragChannel = chIdx;
            dragKey = keyIdx;
            return;
        }

        if (e.clickCount >= 2)
        {
            float t = Mathf.Clamp(XToTime(m.x), 0f, duration);
            int idx = ch.curve.AddKey(t, YToValue(ch, row, m.y));
            selChannel = chIdx;
            selKey = idx;
            drag = DragKind.Key;
            dragChannel = chIdx;
            dragKey = idx;
            return;
        }

        selChannel = chIdx;
        selKey = -1;
    }

    // The framework's context-menu handler ([EventHandlerAttribute(EventType.MouseDown, 0)])
    // consumes every button-1 MouseDown over a node BEFORE NodeGUI runs, so right-click delete
    // must be a pre-GUI handler at a lower priority. Use() fires only on an actual key/marker
    // hit, so right-clicking elsewhere still opens the normal node context menu.
    [EventHandlerAttribute(EventType.MouseDown, -1)]
    static void HandleTimelineRightClick(NodeEditorInputInfo info)
    {
        if (info.inputEvent.button != 1) return;
        var state = info.editorState;
        if (state == null || !(state.focusedNode is TimelineNode tn)) return;
        Vector2 local = NodeEditor.ScreenToCanvasSpace(state, info.inputPos)
                      - tn.rect.position - new Vector2(0f, HeaderOffsetY);
        if (tn.eventTrackRect.Contains(local))
        {
            int hit = tn.EventMarkerAt(local.x);
            if (hit >= 0)
            {
                tn.RemoveEvent(hit);
                info.inputEvent.Use();
                NodeEditor.RepaintClients();
            }
            return;
        }
        int chIdx = tn.ChannelAt(local);
        if (chIdx < 0) return;
        int keyIdx = tn.KeyAt(chIdx, local);
        if (keyIdx >= 0)
        {
            tn.channels[chIdx].curve.RemoveKey(keyIdx);
            if (tn.selChannel == chIdx) tn.selKey = -1;
            info.inputEvent.Use();
            NodeEditor.RepaintClients();
        }
    }

    void HandleDrag(Vector2 m, Event e)
    {
        switch (drag)
        {
            case DragKind.Playhead:
                playhead = Mathf.Clamp(XToTime(m.x), 0f, duration);
                break;
            case DragKind.Resize:
                nodeWidth = Mathf.Clamp(dragStartWidth + (m.x - dragStartMouse.x), MinNodeWidth, 4000f);
                break;
            case DragKind.Pan:
                view.Pan(m.x - dragStartMouse.x, curveAreaRect.width, EffectiveViewMax());
                dragStartMouse = m;
                break;
            case DragKind.EventMarker:
                if (dragEvent >= 0 && dragEvent < events.Count)
                {
                    // move by time delta from the grab point: stacked (out-of-view) markers
                    // have no true pixel position, so absolute XToTime would teleport them
                    float dt = (m.x - dragStartMouse.x) * view.Span / Mathf.Max(curveAreaRect.width, 1f);
                    events[dragEvent].time = Mathf.Clamp(dragStartEventTime + dt, 0f, duration);
                    markersDirty = true;
                    UpdateMarkerPixels();
                }
                break;
            case DragKind.Key:
                if (ValidKeyDrag())
                {
                    var ch = channels[dragChannel];
                    var row = channelRects[dragChannel];
                    dragKey = ch.curve.MoveKey(dragKey, Mathf.Clamp(XToTime(m.x), 0f, duration), YToValue(ch, row, m.y));
                    selKey = dragKey;
                }
                break;
            case DragKind.TangentIn:
            case DragKind.TangentOut:
                if (ValidKeyDrag())
                {
                    var ch = channels[dragChannel];
                    var row = channelRects[dragChannel];
                    var key = ch.curve.GetKey(dragKey);
                    var keyPx = new Vector2(TimeToX(key.time), ValueToY(ch, row, key.value));
                    // slope = dv/dt = -(dyPx/dxPx) * pxPerSec/pxPerVal; the same formula holds
                    // for both handles as long as the mouse stays on the handle's side of the key
                    float dxp = m.x - keyPx.x;
                    dxp = drag == DragKind.TangentIn ? Mathf.Min(dxp, -1f) : Mathf.Max(dxp, 1f);
                    float dyp = m.y - keyPx.y;
                    float pxPerSec = row.width / Mathf.Max(view.Span, 1e-4f);
                    float pxPerVal = row.height / Mathf.Max(ch.yMax - ch.yMin, 1e-4f);
                    float slope = -(dyp / dxp) * pxPerSec / pxPerVal;
                    // alt-drag breaks the handles apart; a broken key keeps editing per-side
                    if (e.alt || key.broken)
                        ch.curve.SetBrokenTangent(dragKey, slope, drag == DragKind.TangentIn);
                    else
                        ch.curve.SetLinkedTangent(dragKey, slope);
                }
                break;
        }
    }

    bool ValidKeyDrag() =>
        dragChannel >= 0 && dragChannel < channels.Count &&
        dragKey >= 0 && dragKey < channels[dragChannel].curve.KeyCount &&
        dragChannel < channelRects.Length;

    int ChannelAt(Vector2 m)
    {
        for (int i = 0; i < channelRects.Length && i < channels.Count; i++)
            if (channelRects[i].Contains(m)) return i;
        return -1;
    }

    int KeyAt(int chIdx, Vector2 m)
    {
        var ch = channels[chIdx];
        var row = channelRects[chIdx];
        for (int k = 0; k < ch.curve.KeyCount; k++)
        {
            var key = ch.curve.GetKey(k);
            // out-of-view keys aren't drawn; don't let them win hit-testing either
            if (key.time < view.viewStart || key.time > view.viewEnd) continue;
            var p = new Vector2(TimeToX(key.time), ValueToY(ch, row, key.value));
            if ((p - m).magnitude <= KeyHitRadius) return k;
        }
        return -1;
    }

    int EventMarkerAt(float mx)
    {
        for (int i = 0; i < events.Count && i < markerPixels.Length; i++)
            if (Mathf.Abs(eventTrackRect.x + markerPixels[i] - mx) <= 8f) return i;
        return -1;
    }

    // ---------------------------------------------------------------- mutations

    void AddChannel()
    {
        var ch = new TimelineChannel
        {
            id = nextChannelId++,
            curve = TimelineCurve.DefaultFlat(duration),
        };
        ch.name = $"Signal {nextChannelId}";
        channels.Add(ch);
        EnsurePorts();
        ComputeRects();
    }

    void RemoveChannel(int index)
    {
        if (index < 0 || index >= channels.Count) return;
        channels.RemoveAt(index);
        if (selChannel == index) { selChannel = -1; selKey = -1; }
        else if (selChannel > index) selChannel--;
        if (dragChannel == index) drag = DragKind.None;
        else if (dragChannel > index) dragChannel--;
        EnsurePorts();
        ComputeRects();
    }

    protected override void OnDelete()
    {
        if (audioBound && Application.isPlaying && AudioTrackManager.Instance != null)
        {
            AudioTrackManager.Instance.ReleaseSource(this);
        }
        if (waveformTex != null)
        {
            waveformTex.Release();
            waveformTex = null;
        }
    }

    void RemoveEvent(int index)
    {
        if (index < 0 || index >= events.Count) return;
        events.RemoveAt(index);
        if (dragEvent == index) drag = DragKind.None;
        else if (dragEvent > index) dragEvent--;
        EnsurePorts();
        markersDirty = true;
        UpdateMarkerPixels();
    }

    // ---------------------------------------------------------------- scroll-zoom pre-emption

    // Canvas zoom is a pre-GUI ScrollWheel handler at default priority 50 that never consumes
    // the event. This runs just before it: when the pointer is over a TimelineNode's timeline
    // region, zoom the timeline view instead and consume the event so the canvas doesn't zoom.
    [EventHandlerAttribute(EventType.ScrollWheel, 40)]
    static void HandleTimelineScroll(NodeEditorInputInfo info)
    {
        var state = info.editorState;
        if (state == null || !(state.focusedNode is TimelineNode tn)) return;
        Vector2 canvasPos = NodeEditor.ScreenToCanvasSpace(state, info.inputPos);
        Vector2 local = canvasPos - tn.rect.position - new Vector2(0f, HeaderOffsetY);
        if (!tn.timelineRegionRect.Contains(local) || tn.curveAreaRect.width <= 0f) return;
        float pivot = tn.view.PixelToTime(local.x - tn.curveAreaRect.x, tn.curveAreaRect.width);
        float factor = 1f + Mathf.Clamp(info.inputEvent.delta.y, -4f, 4f) * 0.06f;
        tn.view.ZoomAround(pivot, factor, tn.EffectiveViewMax());
        tn.UpdateMarkerPixels();
        info.inputEvent.Use();
        NodeEditor.RepaintClients();
    }
}

