
using Minis;
using NodeEditorFramework;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;


/* Oddball: a BLE-MIDI motion controller (https://playoddball.com) that turns physical
 * gestures (tap/bounce, shake, twist, rotation, etc.) into MIDI notes and CCs. Unlike a
 * normal controller it streams its CCs continuously, so it is handled specially by
 * MidiDeviceManager: it is excluded from "use control to bind" and its messages are routed
 * only to nodes (like this one) that explicitly subscribe via RegisterOddballHandlers.
 *
 * Reference: https://docs.google.com/document/d/14L2wokwEkl3OIqeRpiXxA0IOLzT7xJSZx7kKNLflzVw
 * Everything is on MIDI channel 1, but we route by device identity rather than channel.
 *
 * Notes (instantaneous velocity pulse on note-on):
 *   1 Tap / Bounce
 *   2 Shake
 *   3 Twist
 * CCs (continuous, 0..1 normalized):
 *   0 Move (smoothed)
 *   1 Spin (smoothed)
 *   2 Free fall
 *   3 X orientation
 *   4 Y orientation
 *   5 Z orientation
 *   6 Energy
 *
 * Identifiers used to recognize the device cross-platform: "ODD", "F62B", "oddball"
 * (matched as a case-insensitive substring of the MIDI port name). On Windows, where the
 * Oddball arrives through a virtual port (loopMIDI + a BLE bridge), name the loopMIDI port
 * so it contains one of those tokens, or call MidiDeviceManager.AddOddballIdentifier(...).
 */
[Node(false, "MIDI/OddballControl")]
public class OddballControlNode : SignalNode
{
    public override string GetID => "OddballControlNode";
    public override string Title { get { return "OddballControl"; } }

    private Vector2 _DefaultSize = new Vector2(240, 50);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    // --- CC output knobs (continuous) ---
    [ValueConnectionKnob("Move", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob moveKnob;
    [ValueConnectionKnob("Spin", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob spinKnob;
    [ValueConnectionKnob("FreeFall", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob freeFallKnob;
    [ValueConnectionKnob("X orient", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob xOrientKnob;
    [ValueConnectionKnob("Y orient", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob yOrientKnob;
    [ValueConnectionKnob("Z orient", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob zOrientKnob;
    [ValueConnectionKnob("Energy", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob energyKnob;

    // --- Note output knobs (instantaneous velocity pulse) ---
    [ValueConnectionKnob("Tap/Bounce", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob tapKnob;
    [ValueConnectionKnob("Shake", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob shakeKnob;
    [ValueConnectionKnob("Twist", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob twistKnob;

    // Latest CC values, written from MIDI callbacks and consumed in DoCalc / the sparkline.
    private readonly Dictionary<int, float> ccValues = new Dictionary<int, float>();
    // Pending note pulses: velocity captured on note-on, emitted to the knob for one frame.
    private readonly Dictionary<int, float> notePulses = new Dictionary<int, float>();
    // Last note velocity / time, used to render a decaying spike on the note sparklines
    // (the raw one-frame pulse is too short for the 30Hz sampler to catch reliably).
    private readonly Dictionary<int, float> noteVelocities = new Dictionary<int, float>();
    private readonly Dictionary<int, float> noteTimes = new Dictionary<int, float>();
    private const float NoteSparklineDecayTau = 0.12f;

    private string nodeInstanceId;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return CCChannel(0, "Move", moveKnob);
        yield return CCChannel(1, "Spin", spinKnob);
        yield return CCChannel(2, "FreeFall", freeFallKnob);
        yield return CCChannel(3, "X orient", xOrientKnob);
        yield return CCChannel(4, "Y orient", yOrientKnob);
        yield return CCChannel(5, "Z orient", zOrientKnob);
        yield return CCChannel(6, "Energy", energyKnob);
        yield return NoteChannel(1, "Tap/Bounce", tapKnob);
        yield return NoteChannel(2, "Shake", shakeKnob);
        yield return NoteChannel(3, "Twist", twistKnob);
    }

    private SignalChannel CCChannel(int cc, string label, ValueConnectionKnob knob)
    {
        return new SignalChannel
        {
            outputKnob = knob,
            getValue   = () => GetCC(cc),
            label      = label,
        };
    }

    private SignalChannel NoteChannel(int note, string label, ValueConnectionKnob knob)
    {
        return new SignalChannel
        {
            outputKnob = knob,
            getValue   = () => NoteDisplayValue(note),
            label      = label,
        };
    }

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();

        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.RegisterOddballHandlers(
                nodeInstanceId, OnOddballCC, OnOddballNoteOn);
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

    // Routed from MidiDeviceManager for any Oddball device, regardless of channel.
    void OnOddballCC(MidiValueControl cc, float value)
    {
        ccValues[cc.controlNumber] = value;
    }

    void OnOddballNoteOn(MidiNoteControl note, float velocity)
    {
        // Knob output is a single-frame pulse; the sparkline reads a decaying copy.
        notePulses[note.noteNumber] = velocity;
        noteVelocities[note.noteNumber] = velocity;
        noteTimes[note.noteNumber] = Time.time;
    }

    private float GetCC(int cc)
    {
        return ccValues.TryGetValue(cc, out float v) ? v : 0f;
    }

    // Raw instantaneous: emit the velocity once, then clear so it's 0 on subsequent frames.
    private float ConsumeNotePulse(int note)
    {
        notePulses.TryGetValue(note, out float v);
        notePulses[note] = 0f;
        return v;
    }

    // Exponentially-decaying velocity since the last note-on, for the sparkline trace only.
    private float NoteDisplayValue(int note)
    {
        if (!noteTimes.TryGetValue(note, out float t)) return 0f;
        float dt = Time.time - t;
        if (dt < 0f) return 0f;
        noteVelocities.TryGetValue(note, out float vel);
        return vel * Mathf.Exp(-dt / NoteSparklineDecayTau);
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        bool connected = MidiDeviceManager.Instance != null &&
                         MidiDeviceManager.Instance.IsOddballConnected();
        GUILayout.Label(connected ? "Oddball connected" : "Oddball not found");

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        moveKnob.SetValue(GetCC(0));
        spinKnob.SetValue(GetCC(1));
        freeFallKnob.SetValue(GetCC(2));
        xOrientKnob.SetValue(GetCC(3));
        yOrientKnob.SetValue(GetCC(4));
        zOrientKnob.SetValue(GetCC(5));
        energyKnob.SetValue(GetCC(6));

        tapKnob.SetValue(ConsumeNotePulse(1));
        shakeKnob.SetValue(ConsumeNotePulse(2));
        twistKnob.SetValue(ConsumeNotePulse(3));

        return true;
    }
}
