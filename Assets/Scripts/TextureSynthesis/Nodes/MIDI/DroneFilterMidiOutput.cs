
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

        midiDevices = new List<Minis.MidiDevice>();
        var match = new InputDeviceMatcher().WithInterface("Minis");
        foreach (InputDevice device in InputSystem.devices){
            if (match.MatchPercentage(device.description) > 0)
                midiDevices.Add(device as Minis.MidiDevice);
        }

        // Register devices new
        InputSystem.onDeviceChange += OnDeviceAdded;
        SetSize();
    }

    private void OnDeviceAdded(InputDevice device, InputDeviceChange change)
    {
        var midiDevice = device as Minis.MidiDevice;
        if (midiDevice == null || change != InputDeviceChange.Added) 
            return;
        midiDevices.Add(midiDevice);
        Debug.Log("MIDI Device added");
        if (bound && boundDevice == null)
        {
            if (midiDevice.channel == channel){
                boundDevice = midiDevice;
                midiDevice.onWillControlChange += ReceiveMIDIMessage;
            }
        }
        else if (binding){
            midiDevice.onWillControlChange += OnBindEvent;
        }
    }

    private void foo(MidiNoteControl arg1, float arg2)
    {
        boundDevice.onWillNoteOn -= foo;
    }

    void BeginBindingMinis()
    {
        binding = true;
        foreach (var device in midiDevices)
        {
            device.onWillControlChange += OnBindEvent;
        }
    }

    private void OnBindEvent(MidiValueControl cc, float value)
    {
        foreach (var device in midiDevices)
        {
            device.onWillControlChange -= OnBindEvent;
        }
        boundDevice = cc.device as Minis.MidiDevice;
        boundDevice.onWillNoteOn += foo;
        boundDevice.onWillControlChange += ReceiveMIDIMessage;
        channel = boundDevice.channel;
        controlID = cc.controlNumber;
        binding = false;
        bound = true;
    }

    void ReceiveMIDIMessage(Minis.MidiValueControl cc, float value)
    {
        if (cc.controlNumber == controlID)
        {
            rawMIDIValue = value;
        }
    }

    public void ShowMidiDevicesGUI()
    {
        foreach (var device in midiDevices)
        {
            if (device == null) continue;
            var deviceLabel = $"ID: {device.deviceId}, Name: {device.displayName} ({device.shortDisplayName}), {device.name}";
            if (device.deviceId == boundDevice?.deviceId)
            {
                deviceLabel = "(bound) " + deviceLabel;
                GUILayout.Label(deviceLabel);
            }
            else
            {
                GUILayout.Label(deviceLabel);
                if (GUILayout.Button("Bind"))
                {
                    boundDevice = device;
                    channel = device.channel;
                    bound = true;
                }
            }
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        ShowMidiDevicesGUI();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool DoCalc()
    {
        float val = rawMIDIValue;
        if (boundDevice != null)
        {
            RtMidi.Unmanaged.
        }
        return true;
    }
}
