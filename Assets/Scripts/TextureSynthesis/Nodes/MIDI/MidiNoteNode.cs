using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minis-based MIDI note input following KeySignal's shape: one-tick pressed/released
/// pulses, a level-style held bool, and a float output (note velocity while held, or a
/// flat 1 with "use velocity" off). Bind waits for the next note-on on any non-Oddball
/// device; the device product is captured at bind time so reloads re-register exactly.
/// Feed `pressed` or `held` into EnvelopeGenerator's trigger for shaped MIDI signals.
/// </summary>
[Node(false, "MIDI/MIDINote")]
public class MidiNoteNode : SignalNode
{
    public override string GetID => "MidiNoteNode";
    public override string Title { get { return "MIDINote"; } }

    private Vector2 _DefaultSize = new Vector2(165, 110);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("Out", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob signalOutputKnob;

    [ValueConnectionKnob("pressed", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob pressedKnob;

    [ValueConnectionKnob("held", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob heldKnob;

    [ValueConnectionKnob("released", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob releasedKnob;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = signalOutputKnob,
            getValue   = () => signalOutputKnob.GetValue<float>(),
            label      = "Out",
        };
    }

    public bool bound = false;
    public int channel;
    public int noteNumber;
    // Captured at bind time so the exact device re-resolves on reload even with
    // multiple MIDI devices connected. Empty on legacy saves.
    public string deviceProduct = "";
    public bool useVelocity = true;

    bool binding = false;
    private string nodeInstanceId;

    // MIDI callbacks land between frames (InputSystem update); latched here, consumed in DoCalc
    [System.NonSerialized] private bool noteHeld;
    [System.NonSerialized] private bool pressedPending;
    [System.NonSerialized] private bool releasedPending;
    [System.NonSerialized] private float lastVelocity;
    // One-tick pulses stay true for every Calculate within their frame: OnNodeChange can
    // re-run Calculate in-frame, and consuming on the first call would hide the pulse
    // from the re-runs downstream nodes actually read.
    [System.NonSerialized] private int pressedFrame = -1;
    [System.NonSerialized] private int releasedFrame = -1;

    static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    static string NoteName(int n) => $"{NoteNames[((n % 12) + 12) % 12]}{n / 12 - 1}";

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();
        if (bound)
        {
            MidiDeviceManager.Instance.RegisterNoteHandlers(nodeInstanceId, channel, noteNumber, OnNoteOn, OnNoteOff);
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
        MidiDeviceManager.Instance.BeginNoteBinding(nodeInstanceId, OnBindComplete);
    }

    private void OnBindComplete(MidiDevice device, int deviceChannel, int deviceNoteNumber)
    {
        channel = deviceChannel;
        noteNumber = deviceNoteNumber;
        deviceProduct = device != null ? (device.description.product ?? "") : "";
        binding = false;
        bound = true;
        MidiDeviceManager.Instance.RegisterNoteHandlers(nodeInstanceId, channel, noteNumber, OnNoteOn, OnNoteOff);
        // The note-on that completed the bind is itself a press
        noteHeld = true;
        pressedPending = true;
    }

    void Unbind()
    {
        MidiDeviceManager.Instance.UnregisterNoteHandlers(nodeInstanceId, channel, noteNumber);
        bound = false;
        deviceProduct = "";
        noteHeld = false;
        pressedPending = false;
        releasedPending = false;
        lastVelocity = 0f;
    }

    void OnNoteOn(MidiNoteControl note, float velocity)
    {
        if (note.noteNumber != noteNumber) return;
        noteHeld = true;
        pressedPending = true;
        lastVelocity = velocity;
    }

    void OnNoteOff(MidiNoteControl note)
    {
        if (note.noteNumber != noteNumber) return;
        noteHeld = false;
        releasedPending = true;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind note"))
            {
                BeginBinding();
            }
        }
        else if (binding)
        {
            GUILayout.Label("Play a note to bind");
            if (GUILayout.Button("Cancel"))
            {
                MidiDeviceManager.Instance.CancelBinding();
                binding = false;
            }
        }
        else
        {
            // Fixed width so the label region doesn't jitter as the velocity digits change
            GUILayout.Label($"ch{channel} {NoteName(noteNumber)} ({noteNumber}): {lastVelocity:0.00}", GUILayout.Width(150));
            if (GUILayout.Button("Unbind"))
            {
                Unbind();
            }
        }
        useVelocity = RTEditorGUI.Toggle(useVelocity, new GUIContent("Use velocity", "Output note velocity while held instead of a flat 1"));
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        pressedKnob.DisplayLayout();
        heldKnob.DisplayLayout();
        releasedKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (pressedPending)
        {
            pressedFrame = Time.frameCount;
            pressedPending = false;
        }
        if (releasedPending)
        {
            releasedFrame = Time.frameCount;
            releasedPending = false;
        }
        pressedKnob.SetValue(Time.frameCount == pressedFrame);
        heldKnob.SetValue(noteHeld);
        releasedKnob.SetValue(Time.frameCount == releasedFrame);
        signalOutputKnob.SetValue(noteHeld ? (useVelocity ? lastVelocity : 1f) : 0f);
        return true;
    }
}
