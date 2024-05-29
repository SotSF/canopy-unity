
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;



[Node(false, "MIDI/MIDIControlArrayMinis")]
public class MinisControlArrayNode : TickingNode
{
    public override string GetID => "MinisControlArrayNode";
    public override string Title { get { return "MinisControlArray"; } }
    private const int rescaleControlsHeight = 45;
    private const int nodeBaseHeight = 20;
    private const int controlBaseHeight = 60;

    private Vector2 _DefaultSize = new Vector2(160,
        nodeBaseHeight
        + 1 * controlBaseHeight);
    public override Vector2 DefaultSize => _DefaultSize;

    [Serializable]
    public class BoundMidiControl {

        public static ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute("Value", Direction.Out, typeof(float), NodeSide.Right);
        public static ValueConnectionKnobAttribute minKnobAttrib = new ValueConnectionKnobAttribute("rescaleMin", Direction.In, typeof(float), NodeSide.Left);
        public static ValueConnectionKnobAttribute maxKnobAttrib = new ValueConnectionKnobAttribute("rescaleMax", Direction.In, typeof(float), NodeSide.Left);
        public float rawMIDIValue;
        public float rescaleMin = 0;
        public float rescaleMax = 1;
        public bool rescale = false;
        public int controlID;
        public bool binding = false;
        public bool bound = false;
        public bool deleted = false;
        public int controlIndex;
        public Node parent;
        public ValueConnectionKnob minKnob;
        public ValueConnectionKnob maxKnob;
        public ValueConnectionKnob outputKnob;

        public void AddOutputPort()
        {
            outputKnob = parent.CreateValueConnectionKnob(outKnobAttribs);
        }
        public void SetRescalePorts()
        {
            if (rescale)
            {
                AddRescalePorts();
            }
            else
            {
                RemoveRescalePorts();
            }
        }

        public void AddRescalePorts()
        {
            minKnob = parent.CreateValueConnectionKnob(minKnobAttrib);
            maxKnob = parent.CreateValueConnectionKnob(maxKnobAttrib);
        }

        public void OnDelete()
        {
            if (minKnob != null) RemoveRescalePorts();
            parent.DeleteConnectionPort(outputKnob);
        }
        
        public void RemoveRescalePorts()
        {
            parent.DeleteConnectionPort(minKnob);
            parent.DeleteConnectionPort(maxKnob);
        }

        public BoundMidiControl(Node parent){
            this.parent = parent;
        }
    }

    public int channel;
    public List<BoundMidiControl> controls;

    public int numControls => controls.Count;

    private List<Minis.MidiDevice> midiDevices;
    private Minis.MidiDevice boundDevice;

    private int bindingIndex = 0;

    private void Awake()
    {
        if (controls == null){
            controls = new List<BoundMidiControl>();
            controls.Add(new BoundMidiControl(this));
        }
        midiDevices = new List<Minis.MidiDevice>();
        var match = new InputDeviceMatcher().WithInterface("Minis");
        foreach (InputDevice device in InputSystem.devices){
            if (match.MatchPercentage(device.description) > 0)
                midiDevices.Add(device as Minis.MidiDevice);
        }

        // Register devices new
        InputSystem.onDeviceChange += OnDeviceAdded;
    }

    private void OnDeviceAdded(InputDevice device, InputDeviceChange change)
    {
        var midiDevice = device as Minis.MidiDevice;
        if (midiDevice == null || change != InputDeviceChange.Added) 
            return;
        midiDevices.Add(midiDevice);
        Debug.Log("MIDI Device added");
        if (controls.Any(cc => cc.bound) && boundDevice == null)
        {
            if (midiDevice.channel == channel){
                boundDevice = midiDevice;
                midiDevice.onWillControlChange += ReceiveMIDIMessage;
            }
        }
        else if (controls.Any(cc => cc.binding)){
            midiDevice.onWillControlChange += OnBindEvent;
        }
    }

    private void foo(MidiNoteControl arg1, float arg2)
    {
        boundDevice.onWillNoteOn -= foo;
    }

    void BeginBindingMinis()
    {
        controls[bindingIndex].binding = true;
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
        controls[bindingIndex].controlID = cc.controlNumber;
        controls[bindingIndex].binding = false;
        controls[bindingIndex].bound = true;
        controls[bindingIndex].AddOutputPort();
        controls.Add(new BoundMidiControl(this));
        SetSize();
    }

    void ReceiveMIDIMessage(Minis.MidiValueControl cc, float value)
    {
        foreach (var control in controls)
        {
            if (cc.controlNumber == control.controlID)
            {
                control.rawMIDIValue = value;
            }
        }
    }

    private void SetSize()
    {
        _DefaultSize = new Vector2(160,
             nodeBaseHeight
            + numControls * controlBaseHeight
            + controls.Where(cc => cc.rescale).Sum(i => rescaleControlsHeight)
        );
    }

    public override void NodeGUI()
    {
        bool resized = false;
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        foreach (var control in controls)
        {

            if (!control.bound && !control.binding)
            {
                if (GUILayout.Button("Bind input knob"))
                {
                    bindingIndex = controls.IndexOf(control);
                    BeginBindingMinis();
                }
            }
            else
            {
                if (control.bound)
                {
                    GUILayout.BeginHorizontal();
                    string scaledValue = control.rescale ? "" : "";
                    string label = string.Format(" {0} ctrl {1}: {2:0.00}", channel.ToString(), control.controlID, control.rawMIDIValue);
                    // GUILayout.Label(label);
                    GUIContent content = new GUIContent(label);
                    control.outputKnob.DisplayLayout(content);
                    if (GUILayout.Button("Unbind"))
                    {
                        control.controlID = 0;
                        control.bound = false;
                        control.deleted = true;
                        control.OnDelete();
                        resized = true;
                    }
                    GUILayout.EndHorizontal();
                    // Rescale float inputs
                    bool lastRescale = control.rescale;
                    control.rescale = RTEditorGUI.Toggle(control.rescale, "Rescale value");
                    if (lastRescale != control.rescale)
                    {
                        control.SetRescalePorts();
                        resized = true;
                    }
                    if (control.rescale && dynamicConnectionPorts.Count >= 2)
                    {
                        FloatKnobOrField(GUIContent.none, ref control.rescaleMin, (ValueConnectionKnob)control.minKnob);
                        FloatKnobOrField(GUIContent.none, ref control.rescaleMax, (ValueConnectionKnob)control.maxKnob);
                    }
                }
                else
                {
                    GUILayout.Label("Use control to bind");
                }
            }
            // Horizontal rule
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("─────────────────");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        controls.RemoveAll(cc => cc.deleted);
        int activeCount = controls.Sum(cc => cc.bound || cc.binding ? 1 : 0);
        if (activeCount == 0 && boundDevice != null)
        {
            boundDevice.onWillControlChange -= ReceiveMIDIMessage;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        if (resized)
        {
            SetSize();
        }

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        int i = 0;
        foreach (var control in controls)
        {
            if (control.bound)
            {
                float val = control.rawMIDIValue;
                if (control.rescale)
                {
                    val = Mathf.Lerp(control.rescaleMin, control.rescaleMax, control.rawMIDIValue);
                }
                if (control.outputKnob == null)
                {
                    control.outputKnob = (ValueConnectionKnob)dynamicConnectionPorts[i];
                    SetSize();
                }
                control.outputKnob.SetValue(val);
            }
            i++;
        }
        return true;
    }
}
