
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
    public struct MIDIKeyData
    {
        public float value;
        public int note;
        public MidiChannel channel;
    }

    public override string GetID => "CustomMIDIKeyArrayNode";
    public override string Title { get { return "CustomMIDIKeyArray"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    bool binding = false;
    public bool bound = false;

    public List<MIDIKeyData> keys;

    private void Awake()
    {
        if (bound)
        {
            
        }
    }

    void BindMIDIKey(MidiJack.MidiChannel chan, int note, float velocity)
    {
        var newkey = new MIDIKeyData() { channel = chan, note = note, value = velocity };
        keys.Add(newkey);
        MidiMaster.noteOnDelegate -= BindMIDIKey;
        MidiMaster.noteOnDelegate += ReceiveKeyDown;
        MidiMaster.noteOffDelegate += ReceiveKeyUp;
    }

    private void ReceiveKeyUp(MidiChannel channel, int note)
    {
        if (keys.Where(k => k.note == note && k.channel == channel).Count() > 0) { }
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
