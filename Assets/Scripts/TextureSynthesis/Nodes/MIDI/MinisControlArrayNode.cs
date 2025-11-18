
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;



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

    private string nodeInstanceId;
    private int bindingIndex = 0;

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();

        if (controls == null){
            controls = new List<BoundMidiControl>();
            controls.Add(new BoundMidiControl(this));
        }

        // Register any already-bound controls with MidiDeviceManager
        foreach (var control in controls)
        {
            if (control.bound)
            {
                string controlKey = $"{nodeInstanceId}_ctrl{control.controlIndex}";
                MidiDeviceManager.Instance.RegisterControlHandler(controlKey, channel, control.controlID,
                    (cc, value) => ReceiveMIDIMessageForControl(control, cc, value));
            }
        }
    }

    private void OnDestroy()
    {
        // Unregister all controls from MidiDeviceManager
        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.UnregisterNode(nodeInstanceId);
        }
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    void BeginBindingMinis()
    {
        controls[bindingIndex].binding = true;
        MidiDeviceManager.Instance.BeginControlBinding(nodeInstanceId, OnBindComplete);
    }

    private void OnBindComplete(Minis.MidiDevice device, int deviceChannel, int deviceControlID)
    {
        channel = deviceChannel;
        controls[bindingIndex].controlID = deviceControlID;
        controls[bindingIndex].controlIndex = bindingIndex;
        controls[bindingIndex].binding = false;
        controls[bindingIndex].bound = true;
        controls[bindingIndex].AddOutputPort();

        // Register handler for this control
        var control = controls[bindingIndex];
        string controlKey = $"{nodeInstanceId}_ctrl{control.controlIndex}";
        MidiDeviceManager.Instance.RegisterControlHandler(controlKey, channel, control.controlID,
            (cc, value) => ReceiveMIDIMessageForControl(control, cc, value));

        // Add new empty control slot
        controls.Add(new BoundMidiControl(this));
        SetSize();
    }

    void ReceiveMIDIMessageForControl(BoundMidiControl control, Minis.MidiValueControl cc, float value)
    {
        if (cc.controlNumber == control.controlID)
        {
            control.rawMIDIValue = value;
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

    private void FixKnobs()
    {
        int i = 0;
        foreach (var control in controls)
        {
            if (i >= dynamicConnectionPorts.Count) break;
            control.outputKnob = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (control.rescale)
            {
                control.minKnob = (ValueConnectionKnob)dynamicConnectionPorts[++i];
                control.maxKnob = (ValueConnectionKnob)dynamicConnectionPorts[++i];
            }
            i++;
        }
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
                        // Unregister from MidiDeviceManager
                        string controlKey = $"{nodeInstanceId}_ctrl{control.controlIndex}";
                        MidiDeviceManager.Instance.UnregisterControlHandler(controlKey, channel, control.controlID);

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
                        if (control.minKnob == null || control.maxKnob == null)
                        {
                            FixKnobs();
                        }
                        FloatKnobOrField(GUIContent.none, ref control.rescaleMin, control.minKnob);
                        FloatKnobOrField(GUIContent.none, ref control.rescaleMax, control.maxKnob);
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
    
    public override bool DoCalc()
    {
        int i = 0;
        foreach (var control in controls)
        {
            if (control.bound)
            {
                if (control.rescale && control.minKnob != null && control.maxKnob != null)
                {
                    if (control.minKnob.connected())
                    {
                        control.rescaleMin = control.minKnob.GetValue<float>();
                    }
                    if (control.maxKnob.connected())
                    {
                        control.rescaleMax = control.maxKnob.GetValue<float>();
                    }
                }
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
