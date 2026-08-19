using System;
using System.Collections.Generic;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Record/playback deck for canopy output (.canrec files) — one node, DAW-style.
/// Create a new recording (named) or load an existing one; play it back as a texture
/// output, and punch RECORD anywhere: capture overwrites exactly the frames the
/// playhead traverses (multiple takes to dial in one section) and appends seamlessly
/// past the end. While recording the node outputs the live input for monitoring;
/// arming record starts the transport rolling.
///
/// Capture: AsyncGPUReadback at up to 60 fps (excess discarded), alpha baked over
/// black, streamed to disk — memory stays flat. Timing: existing frames keep their
/// timestamps when punched (content-only overwrite); appended frames get real capture
/// times. The `time` input slaves both playback AND the punch position to an external
/// clock (e.g. a TimelineNode's time output) for frame-exact AV sync.
/// Transport event pulses (top edge) mirror the buttons, same as Timeline/AudioTrack.
/// </summary>
[Node(false, "Canopy/CanopyTrack")]
public class CanopyTrackNode : TickingNode
{
    public override string GetID => "CanopyTrackNode";
    public override string Title { get { return "CanopyTrack"; } }

    private Vector2 _DefaultSize = new Vector2(270, 260);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("in", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("playPause", Direction.In, typeof(bool), NodeSide.Top, 60)]
    public ValueConnectionKnob playPauseKnob;

    [ValueConnectionKnob("restart", Direction.In, typeof(bool), NodeSide.Top, 100)]
    public ValueConnectionKnob restartKnob;

    [ValueConnectionKnob("back5s", Direction.In, typeof(bool), NodeSide.Top, 140)]
    public ValueConnectionKnob back5sKnob;

    [ValueConnectionKnob("record", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob recordKnob;

    [ValueConnectionKnob("time", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob timeKnob;

    [ValueConnectionKnob("out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    const float CaptureFps = 60f;   // frames above this rate are discarded, not resampled

    public string recordingName = "";
    public bool recordingBound = false;
    public bool playing = false;
    public bool loop = false;
    public float savedPlayhead = 0f;

    [NonSerialized] private CanopyRecordingFile file;
    [NonSerialized] private bool isNewUncreated;   // bound to a name whose file appears on first recorded frame
    [NonSerialized] private Texture2D frameTex;
    [NonSerialized] private byte[] frameBuffer;
    [NonSerialized] private byte[] rgbBuffer;
    [NonSerialized] private int currentFrame = -1;
    [NonSerialized] private float playhead;
    [NonSerialized] private int selectedIdx;
    [NonSerialized] private bool scrubbing;
    [NonSerialized] private string newNameField = "";
    // recording state
    [NonSerialized] private bool recording;
    [NonSerialized] private bool stopRequested;
    [NonSerialized] private int pendingReadbacks;
    [NonSerialized] private float lastCaptureTime = float.NegativeInfinity;
    [NonSerialized] private int lastWrittenIndex = -1;
    [NonSerialized] private readonly Queue<float> pendingTimes = new Queue<float>();
    // GUI latches intent; state changes only in DoCalc's once-per-frame Update tick
    [NonSerialized] private bool pendingPlayPause, pendingRestart, pendingBack5, pendingRecordToggle;
    [NonSerialized] private float pendingSeek = -1f;
    [NonSerialized] private int lastPulseFrame = -1;

    public override void DoInit()
    {
        if (recordingBound && Application.isPlaying)
        {
            BindExisting();
        }
    }

    private void OnDestroy()
    {
        CloseFile();
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    // ---------------------------------------------------------------- bind/unbind

    void BindExisting()
    {
        CloseFile();
        file = RecordingManager.Instance != null ? RecordingManager.Instance.Open(recordingName, writable: true) : null;
        if (file == null)
        {
            Unbind();
            return;
        }
        isNewUncreated = false;
        AllocatePlaybackResources(file.Width, file.Height);
        playhead = Mathf.Clamp(savedPlayhead, 0f, file.Duration);
    }

    void AllocatePlaybackResources(int width, int height)
    {
        frameBuffer = new byte[CanopyRecordingFormat.FrameSize(width, height)];
        if (frameTex != null) UnityEngine.Object.DestroyImmediate(frameTex);
        frameTex = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };
        currentFrame = -1;
    }

    void CloseFile()
    {
        recording = false;
        stopRequested = false;
        pendingTimes.Clear();
        file?.Dispose();
        file = null;
        if (frameTex != null)
        {
            UnityEngine.Object.DestroyImmediate(frameTex);
            frameTex = null;
        }
        currentFrame = -1;
        if (RecordingManager.Instance != null) RecordingManager.Instance.Refresh();
    }

    void Unbind()
    {
        CloseFile();
        recordingBound = false;
        recordingName = "";
        isNewUncreated = false;
        playing = false;
        savedPlayhead = 0f;
    }

    // ---------------------------------------------------------------- recording

    void ToggleRecord(Texture input)
    {
        if (recording)
        {
            recording = false;
            stopRequested = true;
            if (pendingReadbacks == 0) FinishPunch();
            return;
        }
        // Every refusal logs: a record request that silently does nothing is undebuggable
        if (!Application.isPlaying || !recordingBound)
        {
            Debug.LogWarning("CanopyTrack: record requested but no recording is bound.");
            return;
        }
        if (input == null)
        {
            Debug.LogWarning("CanopyTrack: record requested but the 'in' port has no texture — connect the frames to record.");
            return;
        }
        if (file == null)
        {
            if (RecordingManager.Instance == null)
            {
                Debug.LogWarning("CanopyTrack: record requested but RecordingManager is unavailable.");
                return;
            }
            if (!isNewUncreated) return;
            // First frames of a brand-new recording define its dimensions
            file = RecordingManager.Instance.CreateFile(recordingName, input.width, input.height, CaptureFps);
            AllocatePlaybackResources(input.width, input.height);
            isNewUncreated = false;
            playhead = 0f;
        }
        if (input.width != file.Width || input.height != file.Height)
        {
            Debug.LogWarning($"CanopyTrack: input is {input.width}x{input.height} but recording is {file.Width}x{file.Height}; not recording.");
            return;
        }
        lastCaptureTime = float.NegativeInfinity;
        lastWrittenIndex = -1;
        stopRequested = false;
        recording = true;
        playing = true;   // arming record rolls the transport
    }

    void FinishPunch()
    {
        stopRequested = false;
        file?.Flush();
        currentFrame = -1;   // punched frames must re-read from disk, not the stale cache
        if (RecordingManager.Instance != null) RecordingManager.Instance.Refresh();
    }

    void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        pendingReadbacks--;
        float t = pendingTimes.Count > 0 ? pendingTimes.Dequeue() : 0f;
        if (file != null && !request.hasError)
        {
            var data = request.GetData<byte>(); // RGBA32, tightly packed, bottom row first
            int pixels = file.Width * file.Height;
            if (data.Length >= pixels * 4)
            {
                if (rgbBuffer == null || rgbBuffer.Length != pixels * 3)
                    rgbBuffer = new byte[pixels * 3];
                // Bake alpha over black: content alpha'd out live shouldn't survive in the take
                for (int i = 0; i < pixels; i++)
                {
                    int a = data[i * 4 + 3];
                    rgbBuffer[i * 3] = (byte)(data[i * 4] * a / 255);
                    rgbBuffer[i * 3 + 1] = (byte)(data[i * 4 + 1] * a / 255);
                    rgbBuffer[i * 3 + 2] = (byte)(data[i * 4 + 2] * a / 255);
                }
                CommitFrame(t);
            }
        }
        if (stopRequested && pendingReadbacks == 0) FinishPunch();
    }

    // Punch semantics: within existing material, overwrite the frame the playhead was on
    // (plus fill-forward over any frames skipped since the last commit, so no stale
    // content survives inside the punched region); past the end, append with real times.
    void CommitFrame(float t)
    {
        if (t >= file.Duration || file.FrameCount == 0)
        {
            file.AppendFrame(rgbBuffer, t);
            lastWrittenIndex = file.FrameCount - 1;
            return;
        }
        int idx = file.FrameIndexAtTime(t);
        int from = (lastWrittenIndex >= 0 && lastWrittenIndex < idx) ? lastWrittenIndex + 1 : idx;
        for (int i = from; i <= idx; i++)
        {
            file.WriteFrame(i, rgbBuffer);
        }
        lastWrittenIndex = idx;
    }

    // ---------------------------------------------------------------- GUI

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        if (!recordingBound)
        {
            DrawPicker();
        }
        else
        {
            DrawDeck();
        }
        // One row per input: DisplayLayout puts the knob at the label's height on the
        // knob's own side, so two left-side knobs sharing a row stack on top of each other
        GUILayout.BeginHorizontal();
        recordKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        timeKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void DrawPicker()
    {
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to record/play");
            return;
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("New:", GUILayout.Width(34));
        newNameField = GUILayout.TextField(newNameField);
        if (GUILayout.Button("Create", GUILayout.Width(52)))
        {
            string name = SanitizeName(string.IsNullOrEmpty(newNameField)
                ? $"canopy_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
                : newNameField);
            if (RecordingManager.Instance != null && RecordingManager.Instance.Exists(name))
            {
                Debug.LogWarning($"CanopyTrack: '{name}' already exists; load it instead or pick another name.");
            }
            else
            {
                recordingName = name;
                recordingBound = true;
                isNewUncreated = true;   // file materializes on first recorded frame
                savedPlayhead = 0f;
                playhead = 0f;
            }
        }
        GUILayout.EndHorizontal();

        var names = RecordingManager.Instance != null ? RecordingManager.Instance.RecordingNames : new string[0];
        GUILayout.BeginHorizontal();
        GUILayout.Label("Existing:");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open dir", GUILayout.Width(60)))
        {
            RecordingManager.Instance?.OpenRecordingsFolder();
        }
        if (GUILayout.Button("⟳", GUILayout.Width(24)))
        {
            RecordingManager.Instance?.Refresh();
        }
        GUILayout.EndHorizontal();
        if (names.Length == 0)
        {
            GUILayout.Label("(none in Recordings/)");
            return;
        }
        selectedIdx = Mathf.Clamp(selectedIdx, 0, names.Length - 1);
        selectedIdx = GUILayout.SelectionGrid(selectedIdx, names, 1);
        if (GUILayout.Button("Load"))
        {
            recordingName = names[selectedIdx];
            recordingBound = true;
            savedPlayhead = 0f;
            BindExisting();
        }
    }

    void DrawDeck()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(recordingName + (isNewUncreated ? " (new — record to create)" : ""));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open dir", GUILayout.Width(60)))
        {
            RecordingManager.Instance?.OpenRecordingsFolder();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(playing ? "❚❚" : "▶", GUILayout.Width(30))) pendingPlayPause = true;
        if (GUILayout.Button("⏮", GUILayout.Width(30))) pendingRestart = true;
        if (GUILayout.Button("↶5", GUILayout.Width(30))) pendingBack5 = true;
        var recStyle = new GUIStyle(GUI.skin.button);
        if (recording) recStyle.normal.textColor = recStyle.hover.textColor = new Color(1f, 0.3f, 0.25f);
        if (GUILayout.Button(recording ? "■" : "●", recStyle, GUILayout.Width(30))) pendingRecordToggle = true;
        loop = GUILayout.Toggle(loop, "Loop", GUI.skin.button, GUILayout.Width(44));
        if (GUILayout.Button("Unload", GUILayout.Width(52)))
        {
            Unbind();
            GUILayout.EndHorizontal();
            return;
        }
        GUILayout.EndHorizontal();

        if (file != null)
        {
            bool slaved = timeKnob != null && timeKnob.connected();
            string status = recording ? "● REC " : "";
            GUILayout.Label($"{status}{FormatTime(playhead)} / {FormatTime(file.Duration)}   frame {currentFrame + 1}/{file.FrameCount}{(slaved ? "   ⏱ slaved" : "")}");
        }
        else
        {
            GUILayout.Label("empty — punch ● to start recording");
        }
        // Recording needs frames: surface the missing wire instead of no-op'ing on ●
        if (!textureInputKnob.connected())
        {
            GUILayout.Label("⚠ 'in' not connected — nothing to record");
        }

        // scrub bar: click or drag to seek (latched; the seek applies in DoCalc)
        Rect bar = GUILayoutUtility.GetRect(10f, 14f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint && file != null)
        {
            FillRect(bar, new Color(0.07f, 0.07f, 0.07f, 1f));
            float frac = file.Duration > 0f ? playhead / file.Duration : 0f;
            FillRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(frac), bar.height),
                recording ? new Color(1f, 0.3f, 0.25f, 0.55f) : new Color(0.40f, 0.85f, 1.00f, 0.55f));
        }
        var e = Event.current;
        // Release via rawType: a MouseUp consumed by another handler (type == Used) or
        // released outside the window (type == Ignore) never matches type == MouseUp,
        // which left the scrub pseudo-capture stuck — later clicks anywhere kept seeking
        if (scrubbing && e.rawType == EventType.MouseUp)
        {
            scrubbing = false;
            if (e.type == EventType.MouseUp) e.Use();
        }
        else if (file != null && e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
        {
            scrubbing = true;
            LatchSeek(bar, e.mousePosition.x);
            e.Use();
        }
        else if (scrubbing && e.type == EventType.MouseDrag)
        {
            if (file != null) LatchSeek(bar, e.mousePosition.x);
            e.Use();
        }

        // Monitor the live input while recording, playback otherwise
        Texture preview = recording ? textureInputKnob.GetValue<Texture>() : frameTex;
        if (preview != null)
        {
            GUILayout.Box(preview, GUILayout.MaxWidth(151), GUILayout.MaxHeight(96));
        }
    }

    void LatchSeek(Rect bar, float mouseX)
    {
        float frac = Mathf.Clamp01((mouseX - bar.x) / Mathf.Max(bar.width, 1f));
        pendingSeek = frac * file.Duration;
    }

    static string SanitizeName(string name)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }

    static string FormatTime(float t)
    {
        long tenths = (long)Mathf.Round(Mathf.Max(t, 0f) * 10f);
        return $"{tenths / 600}:{(tenths % 600) / 10f:00.0}";
    }

    static void FillRect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // ---------------------------------------------------------------- calc

    public override bool DoCalc()
    {
        Texture input = textureInputKnob != null ? textureInputKnob.GetValue<Texture>() : null;

        // Transport pulses, playhead advance, and GPU readback requests run once per frame
        // on the Update-phase tick (OnNodeChange can re-run Calculate mid-OnGUI, and GPU
        // work during GUI rendering clobbers the active render target)
        if (Time.frameCount != lastPulseFrame && recordingBound)
        {
            lastPulseFrame = Time.frameCount;

            if (pendingPlayPause || KnobPulse(playPauseKnob)) playing = !playing;
            if (pendingRestart || KnobPulse(restartKnob)) { playhead = 0f; playing = false; }
            if (pendingBack5 || KnobPulse(back5sKnob)) playhead = Mathf.Max(playhead - 5f, 0f);
            if (pendingRecordToggle || KnobPulse(recordKnob)) ToggleRecord(input);
            if (pendingSeek >= 0f) { playhead = pendingSeek; pendingSeek = -1f; }
            pendingPlayPause = pendingRestart = pendingBack5 = pendingRecordToggle = false;

            float duration = file != null ? file.Duration : 0f;
            if (timeKnob != null && timeKnob.connected())
            {
                // exact slave to the external clock (drives the punch position too);
                // while recording, times past the end extend the take instead of wrapping
                float t = Mathf.Max(timeKnob.GetValue<float>(), 0f);
                playhead = (!recording && loop && duration > 0f) ? Mathf.Repeat(t, duration) : t;
            }
            else if (playing)
            {
                playhead += Time.deltaTime;
                if (!recording && playhead >= duration)
                {
                    if (loop && duration > 0f) playhead = Mathf.Repeat(playhead, duration);
                    else { playhead = duration; playing = false; }
                }
            }
            savedPlayhead = playhead;

            if (recording && file != null && input != null
                && Time.time - lastCaptureTime >= 1f / CaptureFps - 0.0005f)
            {
                lastCaptureTime = Time.time;
                pendingTimes.Enqueue(playhead);
                pendingReadbacks++;
                AsyncGPUReadback.Request(input, 0, TextureFormat.RGBA32, OnReadbackComplete);
            }

            // Playback frame upload (skipped while recording: output monitors the input)
            if (!recording && file != null && frameTex != null)
            {
                int frame = file.FrameIndexAtTime(playhead);
                if (frame != currentFrame && frame >= 0 && file.ReadFrame(frame, frameBuffer))
                {
                    frameTex.LoadRawTextureData(frameBuffer);
                    frameTex.Apply(false);
                    currentFrame = frame;
                }
            }
        }

        if (textureOutputKnob != null)
        {
            textureOutputKnob.SetValue<Texture>(recording ? input : frameTex);
        }
        return true;
    }

    static bool KnobPulse(ValueConnectionKnob knob)
    {
        return knob != null && knob.connected() && knob.GetValue<bool>();
    }
}
