
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using RtMidi.LowLevel;


[Node(false, "MIDI/DroneFilterMidiOutput")]
public class DroneFilterMidiOutputNode : TickingNode
{
    public override string GetID => "DroneFilterMidiOutputNode";
    public override string Title { get { return "DroneFilterMidiOutput"; } }

    private Vector2 _DefaultSize = new Vector2(150, 85);
    public override Vector2 DefaultSize => _DefaultSize;

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("inputTex", Direction.Out, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    public float rawMIDIValue;
    public float rescaleMin = 0;
    public float rescaleMax = 1;
    public bool rescale = false;
    public int controlID;

    public int channel;
    private List<Minis.MidiDevice> midiDevices;
    private Minis.MidiDevice boundDevice;
    private void SetSize()
    {
        _DefaultSize = new Vector2(150, rescale ? 125 : 85);
    }

    public override void DoInit()
    {
        channel = 1;
        _probe = new MidiProbe(MidiProbe.Mode.Out);
        SetSize();
    }

    // Scan and open all the available output ports.
    void ScanPorts()
    {
        for (var i = 0; i < _probe.PortCount; i++)
        {
            var name = _probe.GetPortName(i);
            _ports.Add(IsRealPort(name) ? new MidiOutPort(i) : null);
        }
    }

    public void ShowMidiDevicesGUI()
    {
        foreach (var device in _ports)
        {
            if (device == null) continue;
            var deviceLabel = $"MidiPort: {device.ToString()}";
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        ShowMidiDevicesGUI();
        sendMIDI = RTEditorGUI.Toggle(sendMIDI, "Send midi messages to output ports");
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    MidiProbe _probe;
    List<MidiOutPort> _ports = new List<MidiOutPort>();
    public bool sendMIDI = false;
    bool IsRealPort(string name)
    {
        return !name.Contains("Through") && !name.Contains("RtMidi");
    }

    // Close and release all the opened ports.
    void DisposePorts()
    {
        foreach (var p in _ports) p?.Dispose();
        _ports.Clear();
    }

    public override bool DoCalc()
    {
        float val = rawMIDIValue;
        ScanPorts();
        foreach (var port in _ports)
        {
            if (port == null) continue;
            if (rescale)
            {
                val = Mathf.Lerp(rescaleMin, rescaleMax, rawMIDIValue);
            }
            foreach (var controlId in new int[]{ 1, 2, 3, 4, 5 })
            {
                port.SendControlChange(channel, controlID, (byte)(255*(Mathf.Sin(Time.time+controlId)+1)));

            }
        }
        return true;
    }
}
