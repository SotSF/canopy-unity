using NodeEditorFramework;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MIDI clock (0xF8 system-realtime) input. Bind waits for the next clock tick on any
/// port; clock carries no channel, so the bind captures a port name rather than a
/// device channel, and reloads re-register against that port. Outputs a one-tick beat
/// pulse at a selectable musical division, a 0-1 phase ramp through that division, a
/// smoothed BPM estimate, and transport run state from start/continue/stop messages.
/// Ticks keep counting while transport is stopped (most gear keeps sending clock for
/// sync); a Start message resets the tick counter so beats align with the downbeat.
/// </summary>
[Node(false, "MIDI/MIDIClock")]
public class MidiClockNode : SignalNode
{
    public override string GetID => "MidiClockNode";
    public override string Title { get { return "MIDIClock"; } }

    private Vector2 _DefaultSize = new Vector2(180, 110);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    // Values are the MIDI clock tick count per pulse (clock runs at 24 ppqn)
    public enum ClockDivision
    {
        Bar = 96,
        Beat = 24,
        Eighth = 12,
        Sixteenth = 6,
    }

    static readonly ClockDivision[] DivisionValues =
        { ClockDivision.Bar, ClockDivision.Beat, ClockDivision.Eighth, ClockDivision.Sixteenth };
    static readonly string[] DivisionLabels = { "Bar", "Beat", "1/8", "1/16" };

    [ValueConnectionKnob("beat", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob beatKnob;

    [ValueConnectionKnob("running", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob runningKnob;

    [ValueConnectionKnob("phase", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob phaseKnob;

    [ValueConnectionKnob("bpm", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob bpmKnob;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = phaseKnob,
            getValue   = () => phase,
            label      = "phase",
        };
        yield return new SignalChannel
        {
            outputKnob = bpmKnob,
            getValue   = () => bpm,
            label      = "bpm",
        };
    }

    public bool bound = false;
    // RtMidi port name captured at bind time (clock has no channel to key on)
    public string portName = "";
    public ClockDivision division = ClockDivision.Beat;

    bool binding = false;
    private string nodeInstanceId;

    // MIDI callbacks land between frames (Minis pumps RtMidi in EarlyUpdate);
    // latched here, consumed in DoCalc.
    [System.NonSerialized] private long tickCount;
    [System.NonSerialized] private double lastTickPortTime = -1; // driver-stamped, sub-frame precision
    [System.NonSerialized] private double lastTickWallTime = -1; // frame time, for sub-tick phase interpolation
    [System.NonSerialized] private double tickInterval;          // EMA of tick spacing, seconds
    [System.NonSerialized] private bool transportRunning;
    [System.NonSerialized] private float bpm;
    [System.NonSerialized] private float phase;
    [System.NonSerialized] private long lastBeatIndex = -1;
    // One-tick pulse stays true for every Calculate within its frame: OnNodeChange can
    // re-run Calculate in-frame, and consuming on the first call would hide the pulse
    // from the re-runs downstream nodes actually read.
    [System.NonSerialized] private int beatFrame = -1;

    // Reject tick intervals outside plausible clock rates (~10-1500 BPM at 24 ppqn) so
    // transport pauses and first-tick artifacts don't poison the BPM estimate.
    const double MinTickInterval = 60.0 / (1500.0 * 24.0);
    const double MaxTickInterval = 60.0 / (10.0 * 24.0);
    // Per-tick EMA weight; at 24 ppqn this settles in roughly half a second
    const double TickIntervalSmoothing = 0.05;

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();
        if (bound)
        {
            MidiDeviceManager.Instance.RegisterClockHandlers(nodeInstanceId, portName,
                OnClockTick, OnTransportStart, OnTransportContinue, OnTransportStop);
        }
    }

    public override void OnDestroy()
    {
        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.UnregisterNode(nodeInstanceId);
        }
        base.OnDestroy();
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    void BeginBinding()
    {
        binding = true;
        MidiDeviceManager.Instance.BeginClockBinding(nodeInstanceId, OnBindComplete);
    }

    private void OnBindComplete(string boundPortName)
    {
        portName = boundPortName ?? "";
        binding = false;
        bound = true;
        MidiDeviceManager.Instance.RegisterClockHandlers(nodeInstanceId, portName,
            OnClockTick, OnTransportStart, OnTransportContinue, OnTransportStop);
    }

    void Unbind()
    {
        MidiDeviceManager.Instance.UnregisterClockHandlers(nodeInstanceId, portName);
        bound = false;
        portName = "";
        ResetClockState();
    }

    void ResetClockState()
    {
        tickCount = 0;
        lastTickPortTime = -1;
        lastTickWallTime = -1;
        tickInterval = 0;
        transportRunning = false;
        bpm = 0;
        phase = 0;
        lastBeatIndex = -1;
    }

    void OnClockTick(string port, double portTime)
    {
        tickCount++;
        if (lastTickPortTime >= 0)
        {
            double dt = portTime - lastTickPortTime;
            if (dt > MinTickInterval && dt < MaxTickInterval)
            {
                tickInterval = tickInterval <= 0
                    ? dt
                    : tickInterval + TickIntervalSmoothing * (dt - tickInterval);
            }
        }
        lastTickPortTime = portTime;
        lastTickWallTime = Time.realtimeSinceStartupAsDouble;
    }

    void OnTransportStart(string port)
    {
        // The first tick after Start is the downbeat
        tickCount = 0;
        lastBeatIndex = -1;
        transportRunning = true;
    }

    void OnTransportContinue(string port)
    {
        transportRunning = true;
    }

    void OnTransportStop(string port)
    {
        transportRunning = false;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind clock"))
            {
                BeginBinding();
            }
        }
        else if (binding)
        {
            GUILayout.Label("Waiting for clock...");
            if (GUILayout.Button("Cancel"))
            {
                MidiDeviceManager.Instance.CancelBinding();
                binding = false;
            }
        }
        else
        {
            // Fixed width so the label region doesn't jitter as the BPM digits change
            GUILayout.Label($"{portName}: {bpm:0.0} BPM", GUILayout.Width(150));
            if (GUILayout.Button("Unbind"))
            {
                Unbind();
            }
        }
        // SelectionGrid rather than EnumPopup: RTEditorGUI popups are display-only
        // outside the editor, and this UI is primarily used in play mode.
        int divIdx = System.Array.IndexOf(DivisionValues, division);
        if (divIdx < 0) divIdx = 1; // Beat
        int newDivIdx = GUILayout.SelectionGrid(divIdx, DivisionLabels, DivisionValues.Length);
        if (newDivIdx != divIdx)
        {
            division = DivisionValues[newDivIdx];
            // Re-anchor so the division change doesn't fire a spurious beat pulse
            lastBeatIndex = tickCount > 0 ? (tickCount - 1) / (long)division : -1;
        }
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        beatKnob.DisplayLayout();
        runningKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        long div = (long)division;
        if (tickCount > 0)
        {
            // Tick 1 is the downbeat, so beat/phase are computed 0-based from tick 1
            long beatIndex = (tickCount - 1) / div;
            if (beatIndex != lastBeatIndex)
            {
                beatFrame = Time.frameCount;
                lastBeatIndex = beatIndex;
            }

            // Ticks arrive at 24 ppqn (~48 Hz at 120 BPM), coarser than the frame rate;
            // interpolate within the current tick so the phase ramp stays smooth.
            double subTick = 0;
            if (tickInterval > 0 && lastTickWallTime >= 0)
            {
                subTick = System.Math.Min(
                    (Time.realtimeSinceStartupAsDouble - lastTickWallTime) / tickInterval, 1.0);
            }
            phase = (float)((((tickCount - 1) % div) + subTick) / div);
            bpm = tickInterval > 0 ? (float)(60.0 / (tickInterval * 24.0)) : 0f;
        }

        beatKnob.SetValue(Time.frameCount == beatFrame);
        runningKnob.SetValue(transportRunning);
        phaseKnob.SetValue(phase);
        bpmKnob.SetValue(bpm);
        return true;
    }
}
