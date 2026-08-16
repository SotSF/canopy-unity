using NodeEditorFramework;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

/// <summary>
/// Splits a Vector2/3/4 (chosen via radio buttons, which populate the matching
/// dynamic ports) into its float components.
/// </summary>
[Node(false, "Signal/VectorSplit")]
public class VectorSplitNode : TextureSynthNode
{
    public override string GetID => "VectorSplitNode";
    public override string Title { get { return "VectorSplit"; } }

    private Vector2 _DefaultSize = new Vector2(190, 130);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(190, 0);
    public override bool AutoLayout => true;

    public RadioButtonSet vecType = new RadioButtonSet(1, "Vector2", "Vector3", "Vector4");

    static readonly string[] CompNames = { "x", "y", "z", "w" };
    [NonSerialized] float[] values = new float[4];

    int ComponentCount => vecType.IsSelected("Vector2") ? 2 : vecType.IsSelected("Vector4") ? 4 : 3;
    Type VectorType => ComponentCount == 2 ? typeof(Vector2) : ComponentCount == 4 ? typeof(Vector4) : typeof(Vector3);

    public override void DoInit()
    {
        if (values == null || values.Length != 4) values = new float[4];
        EnsurePorts();
    }

    void EnsurePorts()
    {
        int n = ComponentCount;
        bool valid = dynamicConnectionPorts.Count == n + 1;
        if (valid)
        {
            var inPort = (ValueConnectionKnob)dynamicConnectionPorts[0];
            valid = inPort.name == "vec" && inPort.valueType == VectorType;
            for (int i = 0; i < n && valid; i++)
            {
                var port = (ValueConnectionKnob)dynamicConnectionPorts[i + 1];
                valid = port.name == CompNames[i] && port.valueType == typeof(float);
            }
        }
        if (valid) return;

        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            dynamicConnectionPorts[i].ClearConnections();
            DeleteConnectionPort(dynamicConnectionPorts[i]);
        }
        CreateValueConnectionKnob(new ValueConnectionKnobAttribute("vec", Direction.In, VectorType, NodeSide.Left));
        for (int i = 0; i < n; i++)
        {
            CreateValueConnectionKnob(new ValueConnectionKnobAttribute(CompNames[i], Direction.Out, typeof(float), NodeSide.Right));
        }
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
        if (dynamicConnectionPorts.Count > 0)
        {
            ((ValueConnectionKnob)dynamicConnectionPorts[0]).DisplayLayout();
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        for (int i = 0; i < n && i + 1 < dynamicConnectionPorts.Count; i++)
        {
            var knob = (ValueConnectionKnob)dynamicConnectionPorts[i + 1];
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{CompNames[i]}: {values[i]:0.000}");
            knob.SetPosition();
            GUILayout.EndHorizontal();
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
        var inKnob = (ValueConnectionKnob)dynamicConnectionPorts[0];
        if (values == null || values.Length != 4) values = new float[4];
        if (!inKnob.connected())
        {
            for (int i = 0; i < n; i++)
            {
                ((ValueConnectionKnob)dynamicConnectionPorts[i + 1]).ResetValue();
                values[i] = 0f;
            }
            return true;
        }
        if (n == 2)
        {
            var v = inKnob.GetValue<Vector2>();
            values[0] = v.x; values[1] = v.y;
        }
        else if (n == 3)
        {
            var v = inKnob.GetValue<Vector3>();
            values[0] = v.x; values[1] = v.y; values[2] = v.z;
        }
        else
        {
            var v = inKnob.GetValue<Vector4>();
            values[0] = v.x; values[1] = v.y; values[2] = v.z; values[3] = v.w;
        }
        for (int i = 0; i < n; i++)
        {
            ((ValueConnectionKnob)dynamicConnectionPorts[i + 1]).SetValue(values[i]);
        }
        return true;
    }
}
