
using MidiJack;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

//[Node(false, "MIDI/MIDINote")]
public class MIDINoteNode //: TickingNode
{
    // public override string GetID => "MIDINoteNode";
    // public override string Title { get { return "MIDINote"; } }

    // public override Vector2 DefaultSize { get { return new Vector2(150, 110); } }

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    [ValueConnectionKnob("pressed", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob pressedKnob;
    bool pressed;

    [ValueConnectionKnob("held", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob heldKnob;
    bool held;

    [ValueConnectionKnob("released", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob releasedKnob;
    bool released;

    public float value;
    public int note;
    public MidiChannel channel;

    private void Awake()
    {
        if (bound)
        {
            BindMIDINote(channel, note, value);
        }
    }

    void BindMIDINote(MidiJack.MidiChannel chan, int note, float velocity)
    {
        channel = chan;
        this.note = note;
        MidiMaster.noteOnDelegate -= BindMIDINote;
        MidiMaster.noteOnDelegate += ReceiveNoteDown;
        MidiMaster.noteOffDelegate += ReceiveNoteUp;
        binding = false;
        bound = true;
        
    }

    private void ReceiveNoteUp(MidiChannel channel, int note)
    {
        if (channel == this.channel && note == this.note)
        {
            value = 0;
            held = false;
            released = true;
        }
    }

    private void ReceiveNoteDown(MidiChannel channel, int note, float velocity)
    {
        if (channel == this.channel && note == this.note)
        {
            value = velocity;
            pressed = true;
            held = true;
        }
    }

    // public override void NodeGUI()
    // {
    //     GUILayout.BeginHorizontal();
    //     GUILayout.BeginVertical();
    //     if (!bound && !binding)
    //     {
    //         if (GUILayout.Button("Bind input note"))
    //         {
    //             MidiMaster.noteOnDelegate += BindMIDINote;
    //             binding = true;
    //         }
    //     }
    //     else
    //     {
    //         if (bound)
    //         {
    //             string label = string.Format("{0} note {1}: {2:0.00}", channel.ToString(), note, value);
    //             GUILayout.Label(label);
    //             if (GUILayout.Button("Unbind"))
    //             {
    //                 MidiMaster.noteOnDelegate -= ReceiveNoteDown;
    //                 MidiMaster.noteOffDelegate -= ReceiveNoteUp;
    //                 note = 0;
    //                 bound = false;
    //             }
    //         }
    //         else
    //         {
    //             GUILayout.Label("Play note to bind");
    //         }
    //     }
    //     GUILayout.EndVertical();
    //     GUILayout.BeginVertical();
    //     valueKnob.DisplayLayout();
    //     pressedKnob.DisplayLayout();
    //     heldKnob.DisplayLayout();
    //     releasedKnob.DisplayLayout();
    //     GUILayout.EndVertical();
    //     GUILayout.EndHorizontal();

    //     if (GUI.changed)
    //         NodeEditor.curNodeCanvas.OnNodeChange(this);
    // }

    // public override bool Calculate()
    // {
    //     pressedKnob.SetValue(pressed);
    //     if (pressed)
    //         pressed = false;
    //     heldKnob.SetValue(held);
    //     releasedKnob.SetValue(released);
    //     if (released)
    //         released = false;
    //     valueKnob.SetValue(value);
    //     return true;
    // }
}
