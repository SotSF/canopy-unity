
using MidiJack;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

[Node(false, "MIDI/MidiKnob")]
public class MIDIKnobNode : TickingNode
{
    public override string GetID => "MIDIKnobNode";
    public override string Title { get { return "MIDIKnob"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    public float value;
    public bool normalize = true;
    public int knobNumber;
    public MidiChannel channel;

    private void Awake()
    {
        if (bound)
        {
            BindMIDIKnob(channel, knobNumber, value);
        }
    }

    void BindMIDIKnob(MidiJack.MidiChannel chan, int knob, float val)
    {
        channel = chan;
        knobNumber = knob;
        MidiMaster.knobDelegate -= BindMIDIKnob;
        MidiMaster.knobDelegate += ReceiveMIDIMessage;
        binding = false;
        bound = true;
    }

    void ReceiveMIDIMessage(MidiChannel chan, int knob, float val)
    {
        if (chan == channel && knob == knobNumber)
            value = val;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind input knob"))
            {
                MidiMaster.knobDelegate += BindMIDIKnob;
                binding = true;
            }
        }
        else
        {
            if (bound)
            {
                string label = string.Format("{0} knob {1}: {2:0.00}", channel.ToString(), knobNumber, value);
                GUILayout.Label(label);
                if (GUILayout.Button("Unbind"))
                {
                    MidiMaster.knobDelegate -= ReceiveMIDIMessage;
                    knobNumber = 0;
                    bound = false;
                }
            }
            else
            {
                GUILayout.Label("Adjust knob to bind");
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
