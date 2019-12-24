
using MidiJack;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

[Node(false, "MIDI/MIDIKey")]
public class MIDIKeyNode : TickingNode
{
    public override string GetID => "MIDIKeyNode";
    public override string Title { get { return "MIDIKey"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    public float value;
    public int note;
    public MidiChannel channel;

    private void Awake()
    {
        if (bound)
        {
            BindMIDIKey(channel, note, value);
        }
    }

    void BindMIDIKey(MidiJack.MidiChannel chan, int note, float velocity)
    {
        channel = chan;
        this.note = note;
        MidiMaster.noteOnDelegate -= BindMIDIKey;
        MidiMaster.noteOnDelegate += ReceiveKeyDown;
        MidiMaster.noteOffDelegate += ReceiveKeyUp;
        binding = false;
        bound = true;
    }

    private void ReceiveKeyUp(MidiChannel channel, int note)
    {
        if (channel == this.channel && note == this.note)
            value = 0;
    }

    private void ReceiveKeyDown(MidiChannel channel, int note, float velocity)
    {
        if (channel == this.channel && note == this.note)
            value = velocity;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind input key"))
            {
                MidiMaster.noteOnDelegate += BindMIDIKey;
                binding = true;
            }
        }
        else
        {
            if (bound)
            {
                string label = string.Format("{0} note {1}: {2:0.00}", channel.ToString(), note, value);
                GUILayout.Label(label);
                if (GUILayout.Button("Unbind"))
                {
                    MidiMaster.noteOnDelegate -= ReceiveKeyDown;
                    MidiMaster.noteOffDelegate -= ReceiveKeyUp;
                    note = 0;
                    bound = false;
                }
            }
            else
            {
                GUILayout.Label("Press key to bind");
            }
        }
        GUILayout.EndVertical();
        valueKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        valueKnob.SetValue(value);
        return true;
    }
}
