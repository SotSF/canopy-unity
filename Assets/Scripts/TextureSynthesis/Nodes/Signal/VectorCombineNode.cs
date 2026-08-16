using NodeEditorFramework;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

/// <summary>
/// Combines float components into a Vector2/3/4 (chosen via radio buttons, which
/// populate the matching dynamic ports). Unconnected components are editable fields,
/// so this doubles as a constant vector source.
/// </summary>
[Node(false, "Signal/VectorCombine")]
public class VectorCombineNode : TextureSynthNode
{
    public override string GetID => "VectorCombineNode";
    public override string Title { get { return "VectorCombine"; } }

    private Vector2 _DefaultSize = new Vector2(190, 130);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(190, 0);
    public override bool AutoLayout => true;

    public RadioButtonSet vecType = new RadioButtonSet(1, "Vector2", "Vector3", "Vector4");
    public float[] components = new float[4];

    static readonly string[] CompNames = { "x", "y", "z", "w" };

    int ComponentCount => vecType.IsSelected("Vector2") ? 2 : vecType.IsSelected("Vector4") ? 4 : 3;
    Type VectorType => ComponentCount == 2 ? typeof(Vector2) : ComponentCount == 4 ? typeof(Vector4) : typeof(Vector3);

    public override void DoInit()
    {
        if (components == null || components.Length != 4) components = new float[4];
        EnsurePorts();
    }

    void EnsurePorts()
    {
        int n = ComponentCount;
        bool valid = dynamicConnectionPorts.Count == n + 1;
        if (valid)
        {
            for (int i = 0; i < n && valid; i++)
            {
                var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
                valid = port.name == CompNames[i] && port.valueType == typeof(float);
            }
            if (valid)
            {
                var outPort = (ValueConnectionKnob)dynamicConnectionPorts[n];
                valid = outPort.name == "vec" && outPort.valueType == VectorType;
            }
        }
        if (valid) return;

        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            dynamicConnectionPorts[i].ClearConnections();
            DeleteConnectionPort(dynamicConnectionPorts[i]);
        }
        for (int i = 0; i < n; i++)
        {
            CreateValueConnectionKnob(new ValueConnectionKnobAttribute(CompNames[i], Direction.In, typeof(float), NodeSide.Left));
        }
        CreateValueConnectionKnob(new ValueConnectionKnobAttribute("vec", Direction.Out, VectorType, NodeSide.Right));
    }

    public override void NodeGUI()
    {
        EnsurePorts();
        GUILayout.BeginVertical();
        // Snapshot the count BEFORE the radios: a click mutates vecType mid-pass, and the
        // row structure must stay consistent with this pass's Layout (ports rebuild next pass)
        int n = ComponentCount;
        RadioButtonsHorizontal(vecType);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        for (int i = 0; i < n && i < dynamicConnectionPorts.Count; i++)
        {
            var knob = (ValueConnectionKnob)dynamicConnectionPorts[i];
            FloatKnobOrField(CompNames[i], ref components[i], knob);
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        if (dynamicConnectionPorts.Count > n)
        {
            ((ValueConnectionKnob)dynamicConnectionPorts[n]).DisplayLayout();
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        int n = ComponentCount;
        if (dynamicConnectionPorts.Count < n + 1) return true;
        for (int i = 0; i < n; i++)
        {
            var knob = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (knob.connected()) components[i] = knob.GetValue<float>();
        }
        var outKnob = (ValueConnectionKnob)dynamicConnectionPorts[n];
        if (n == 2) outKnob.SetValue(new Vector2(components[0], components[1]));
        else if (n == 3) outKnob.SetValue(new Vector3(components[0], components[1], components[2]));
        else outKnob.SetValue(new Vector4(components[0], components[1], components[2], components[3]));
        return true;
    }
}
