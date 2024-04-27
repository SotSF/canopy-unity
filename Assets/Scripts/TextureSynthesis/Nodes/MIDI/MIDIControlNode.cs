
using MidiJack;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

//[Node(false, "MIDI/MIDIControl")]
public class MIDIControlNode //: TickingNode
{
    // public override string GetID => "MIDIControlNode";
    // public override string Title { get { return "MIDIControl"; } }

    // public override Vector2 DefaultSize { get { return new Vector2(150, rescale ? 125 : 85); } }

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    public float rawMIDIValue;
    public float rescaleMin = 0;
    public float rescaleMax = 1;
    public bool rescale = false;
    public int controlID;
    public MidiChannel channel;

    private void Awake()
    {
        if (bound)
        {
            BindMIDIControl(channel, controlID, rawMIDIValue);
        }
    }

    // void SetRescalePorts()
    // {
    //     if (rescale)
    //     {
    //         ValueConnectionKnobAttribute minKnobAttrib = new ValueConnectionKnobAttribute("rescaleMin", Direction.In, typeof(float), NodeSide.Left);
    //         ValueConnectionKnobAttribute maxKnobAttrib = new ValueConnectionKnobAttribute("rescaleMax", Direction.In, typeof(float), NodeSide.Left);
    //         CreateValueConnectionKnob(minKnobAttrib);
    //         CreateValueConnectionKnob(maxKnobAttrib);
    //     } 
    //     else
    //     {
    //         DeleteConnectionPort(dynamicConnectionPorts[1]);
    //         DeleteConnectionPort(dynamicConnectionPorts[0]);
    //     }
    // }

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
            rawMIDIValue = val;
        }
    }

    // public override void NodeGUI()
    // {
    //     GUILayout.BeginHorizontal();
    //     GUILayout.BeginVertical();
    //     if (!bound && !binding)
    //     {
    //         if (GUILayout.Button("Bind input knob"))
    //         {
    //             MidiMaster.knobDelegate += BindMIDIControl;
    //             binding = true;
    //         }
    //     }
    //     else
    //     {
    //         if (bound)
    //         {
    //             string label = string.Format("{0} ctrl {1}: {2:0.00}", channel.ToString(), controlID, rawMIDIValue);
    //             GUILayout.Label(label);
    //             if (GUILayout.Button("Unbind"))
    //             {
    //                 MidiMaster.knobDelegate -= ReceiveMIDIMessage;
    //                 controlID = 0;
    //                 bound = false;
    //             }
    //         }
    //         else
    //         {
    //             GUILayout.Label("Use control to bind");
    //         }
    //     }

    //     // Rescale float inputs
    //     bool lastRescale = rescale;
    //     rescale = RTEditorGUI.Toggle(rescale, "Rescale value");
    //     // if (lastRescale != rescale)
    //     // {
    //     //     SetRescalePorts();
    //     // }
    //     // if (rescale && dynamicConnectionPorts.Count >= 2)
    //     // {
    //     //     FloatKnobOrField("", ref rescaleMin, (ValueConnectionKnob)dynamicConnectionPorts[0]);
    //     //     FloatKnobOrField("", ref rescaleMax, (ValueConnectionKnob)dynamicConnectionPorts[1]);
    //     // }

    //     GUILayout.EndVertical();
    //     valueKnob.DisplayLayout();
    //     GUILayout.EndHorizontal();

    //     // if (GUI.changed)
    //     //     NodeEditor.curNodeCanvas.OnNodeChange(this);
    // }
    
    // public override bool Calculate()
    // {
    //     float val = rawMIDIValue;
    //     if (rescale)
    //     {
    //         val = Mathf.Lerp(rescaleMin, rescaleMax, rawMIDIValue);
    //     }
    //     valueKnob.SetValue(val);
    //     return true;
    // }
}
