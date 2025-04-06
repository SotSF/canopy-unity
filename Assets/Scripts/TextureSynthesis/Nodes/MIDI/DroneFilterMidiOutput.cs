
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

    private Vector2 _DefaultSize = new Vector2(250, 400);
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
        _DefaultSize = _DefaultSize;
    }

    public override void DoInit()
    {
        channel = 1;
        _probe = new MidiProbe(MidiProbe.Mode.Out);
        SetSize();
        lastSendTime = 0;
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
        for (var i = 0; i < _probe.PortCount; i++)
        {
            var name = _probe.GetPortName(i);
            if(IsRealPort(name))
            {
                GUILayout.Label(name);
            }
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        sendMIDI = RTEditorGUI.Toggle(sendMIDI, "Send midi messages to output ports");
        ShowMidiDevicesGUI();
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
    float maxFrameRate = 10;
    float lastSendTime = 0;
    public override bool DoCalc()
    {
        if (lastSendTime != 0)
        {
            var deltaFrame = Time.time - lastSendTime;
            var fps = 1/deltaFrame;
            if (fps > maxFrameRate)
            {
                return false;
            }
        }
        float val = rawMIDIValue;
        ScanPorts();
        foreach (var port in _ports)
        {
            if (port == null || !sendMIDI) continue;
            for (int i = 1; i < 6; i++)
            {
                // Debug.Log($"{port.ToString()}");
                port.SendControlChange(1, i, (byte)i*20);
            }
        }
        lastSendTime = Time.time;
        return true;
    }
}
