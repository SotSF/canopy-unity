
using MidiJack;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

[Node(false, "MIDI/MIDIControl")]
public class MIDIControlNode : TickingNode
{
    public override string GetID => "MIDIControlNode";
    public override string Title { get { return "MIDIControl"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    public float value;
    public bool normalize = true;
    public int controlID;
    public MidiChannel channel;

    private void Awake()
    {
        if (bound)
        {
            BindMIDIControl(channel, controlID, value);
        }
    }

    void BindMIDIControl(MidiJack.MidiChannel chan, int id, float val)
    {
        channel = chan;
        controlID = id;
        MidiMaster.knobDelegate -= BindMIDIControl;
        MidiMaster.knobDelegate += ReceiveMIDIMessage;
        binding = false;
        bound = true;
    }

    void ReceiveMIDIMessage(MidiChannel chan, int id, float val)
    {
        if (chan == channel && id == controlID)
        { 
            value = val;
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind input knob"))
            {
                MidiMaster.knobDelegate += BindMIDIControl;
                binding = true;
            }
        }
        else
        {
            if (bound)
            {
                string label = string.Format("{0} ctrl {1}: {2:0.00}", channel.ToString(), controlID, value);
                GUILayout.Label(label);
                if (GUILayout.Button("Unbind"))
                {
                    MidiMaster.knobDelegate -= ReceiveMIDIMessage;
                    controlID = 0;
                    bound = false;
                }
            }
            else
            {
                GUILayout.Label("Use control to bind");
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
