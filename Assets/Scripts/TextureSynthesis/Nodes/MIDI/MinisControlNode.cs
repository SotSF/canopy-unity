
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;



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
    private string nodeInstanceId;

    private void SetSize()
    {
        _DefaultSize = new Vector2(150, rescale ? 125 : 85);
    }

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();
        SetSize();

        // If already bound, register with MidiDeviceManager
        if (bound)
        {
            MidiDeviceManager.Instance.RegisterControlHandler(nodeInstanceId, channel, controlID, ReceiveMIDIMessage);
        }
    }

    private void OnDestroy()
    {
        // Unregister from MidiDeviceManager
        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.UnregisterNode(nodeInstanceId);
        }
    }

    private void OnDisable()
    {
        OnDestroy();
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
        MidiDeviceManager.Instance.BeginControlBinding(nodeInstanceId, OnBindComplete);
    }

    private void OnBindComplete(Minis.MidiDevice device, int deviceChannel, int deviceControlID)
    {
        channel = deviceChannel;
        controlID = deviceControlID;
        binding = false;
        bound = true;

        // Register handler with MidiDeviceManager
        MidiDeviceManager.Instance.RegisterControlHandler(nodeInstanceId, channel, controlID, ReceiveMIDIMessage);
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
                    MidiDeviceManager.Instance.UnregisterControlHandler(nodeInstanceId, channel, controlID);
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
        if (rescale && dynamicConnectionPorts.Count >= 2)
        {
            if (((ValueConnectionKnob)dynamicConnectionPorts[0]).connected())
            {
                rescaleMin = ((ValueConnectionKnob)dynamicConnectionPorts[0]).GetValue<float>();
            }
            if (((ValueConnectionKnob)dynamicConnectionPorts[1]).connected())
            {
                rescaleMax = ((ValueConnectionKnob)dynamicConnectionPorts[1]).GetValue<float>();
            }
        }
        float val = rawMIDIValue;
        if (rescale)
        {
            val = Mathf.Lerp(rescaleMin, rescaleMax, rawMIDIValue);
        }
        valueKnob.SetValue(val);
        return true;
    }
}
