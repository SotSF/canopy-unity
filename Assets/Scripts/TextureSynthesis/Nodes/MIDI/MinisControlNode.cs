
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;



[Node(false, "MIDI/MIDIControlMinis")]
public class MinisControlNode : TickingNode
{
    public override string GetID => "MinisControlNode";
    public override string Title { get { return "MinisControl"; } }


    private Vector2 _DefaultSize = new Vector2(150, 85);
    public override Vector2 DefaultSize => _DefaultSize;

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

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
        if (midiDevices.Contains(midiDevice))
            return; // Already added
        midiDevices.Add(midiDevice);
        Debug.Log($"MIDI Device added: ${midiDevice.deviceId}");
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

    void SetRescalePorts()
    {
        if (rescale)
        {
            ValueConnectionKnobAttribute minKnobAttrib = new ValueConnectionKnobAttribute("rescaleMin", Direction.In, typeof(float), NodeSide.Left);
            ValueConnectionKnobAttribute maxKnobAttrib = new ValueConnectionKnobAttribute("rescaleMax", Direction.In, typeof(float), NodeSide.Left);
            CreateValueConnectionKnob(minKnobAttrib);
            CreateValueConnectionKnob(maxKnobAttrib);
        } 
        else
        {
            DeleteConnectionPort(dynamicConnectionPorts[1]);
            DeleteConnectionPort(dynamicConnectionPorts[0]);
        }
        SetSize();
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

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind input knob"))
            {
                BeginBindingMinis();
            }
        }
        else
        {
            if (bound)
            {
                string label = string.Format("{0} ctrl {1}: {2:0.00}", channel.ToString(), controlID, rawMIDIValue);
                GUILayout.Label(label);
                if (GUILayout.Button("Unbind"))
                {
                    boundDevice.onWillControlChange -= ReceiveMIDIMessage;
                    controlID = 0;
                    bound = false;
                }
            }
            else
            {
                GUILayout.Label("Use control to bind");
            }
        }

        // Rescale float inputs
        bool lastRescale = rescale;
        rescale = RTEditorGUI.Toggle(rescale, "Rescale value");
        if (lastRescale != rescale)
        {
            SetRescalePorts();
        }
        if (rescale && dynamicConnectionPorts.Count >= 2)
        {
            FloatKnobOrField(GUIContent.none, ref rescaleMin, (ValueConnectionKnob)dynamicConnectionPorts[0]);
            FloatKnobOrField(GUIContent.none, ref rescaleMax, (ValueConnectionKnob)dynamicConnectionPorts[1]);
        }

        GUILayout.EndVertical();
        valueKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool DoCalc()
    {
        float val = rawMIDIValue;
        if (rescale)
        {
            val = Mathf.Lerp(rescaleMin, rescaleMax, rawMIDIValue);
        }
        valueKnob.SetValue(val);
        return true;
    }
}
