
using MidiJack;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Node(false, "Pattern/CustomMIDIKeyArray")]
public class CustomMIDIKeyArrayNode : Node
{
    public struct MIDINote
    {
        public MIDINote(MidiChannel ch, int n)
        {
            note = n;
            channel = ch;
        }
        public int note;
        public MidiChannel channel;
    }

    public override string GetID => "CustomMIDIKeyArrayNode";
    public override string Title { get { return "CustomMIDIKeyArray"; } }
    public override bool AutoLayout => true;
    public override Vector2 MinSize => noteToValue != null ? new Vector2(120, noteToValue.Count * 30 + 40) : new Vector2(120, 70);

    bool binding = false;
    public bool bound = false;

    public Dictionary<MIDINote, float> noteToValue;

    private void Awake()
    {
        if (bound)
        {
            // Attach delegates
        } else
        {
            noteToValue = new Dictionary<MIDINote, float>();
        }
    }

    void BeginBinding()
    {
        MidiMaster.noteOnDelegate += BindMIDIKey;
        binding = true;
    }
    
    void FinishBinding()
    {
        MidiMaster.noteOnDelegate -= BindMIDIKey;
        MidiMaster.noteOnDelegate += ReceiveKeyDown;
        MidiMaster.noteOffDelegate += ReceiveKeyUp;
        binding = false;
        bound = true;
    }

    void Unbind()
    {
        MidiMaster.noteOnDelegate -= ReceiveKeyDown;
        MidiMaster.noteOffDelegate -= ReceiveKeyUp;
        noteToValue.Clear();
        bound = false;
    }

    void BindMIDIKey(MidiJack.MidiChannel chan, int note, float velocity)
    {
        var newNote = new MIDINote(chan, note);
        noteToValue[newNote] = velocity;
        // Create ports here
    }

    private void ReceiveKeyUp(MidiChannel channel, int note)
    {
        var midiNote = new MIDINote(channel, note);
        if (noteToValue.ContainsKey(midiNote))
        {
            noteToValue[midiNote] = 0;
        }
    }

    private void ReceiveKeyDown(MidiChannel channel, int note, float velocity)
    {
        var midiNote = new MIDINote(channel, note);
        if (noteToValue.ContainsKey(midiNote))
        {
            noteToValue[midiNote] = velocity;
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind input MIDI keys"))
            {
                BeginBinding();
            }
        }
        else
        {
            if (bound)
            {
                if (GUILayout.Button("Unbind"))
                {
                    Unbind();
                }
            }
            else
            {
                GUILayout.Label("Press MIDI keys to bind them");
                if (GUILayout.Button("Finish binding"))
                {
                    FinishBinding();
                }
            }
            // Loop over dynamic ports and show them
            foreach(var note in noteToValue.Keys)
            {
                string label = string.Format("{0} note {1}: {2:0.00}", note.channel.ToString(), note.note, noteToValue[note]);
                GUILayout.Label(label);
            }
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        return true;
    }
}
