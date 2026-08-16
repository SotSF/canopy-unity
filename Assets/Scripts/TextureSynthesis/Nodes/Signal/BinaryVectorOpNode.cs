using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

/// <summary>
/// Elementwise arithmetic on vectors without split/combine chains. A and B types are
/// radio-selected (the framework's connection rules require exact type matches, so ports
/// can't auto-adapt to whatever gets connected). Semantics: scalars broadcast to every
/// lane; mismatched vector sizes align from x and A's unmatched lanes pass through, so
/// (0,1,2,3) + (2,3) = (2,4,2,3). Output type follows A. Unconnected B is an editable
/// constant, making this double as scale/offset with a fixed operand.
/// </summary>
[Node(false, "Signal/VectorOp")]
public class BinaryVectorOpNode : TextureSynthNode
{
    public override string GetID => "BinaryVectorOpNode";
    public override string Title { get { return "VectorOp"; } }

    private Vector2 _DefaultSize = new Vector2(210, 150);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(210, 0);
    public override bool AutoLayout => true;

    public RadioButtonSet aType = new RadioButtonSet(1, "Vec2", "Vec3", "Vec4");
    public RadioButtonSet op = new RadioButtonSet(0, "+", "-", "*", "/");
    public RadioButtonSet bMode = new RadioButtonSet(0, "vector", "scalar");
    public RadioButtonSet bType = new RadioButtonSet(1, "Vec2", "Vec3", "Vec4");
    public float[] bComponents = new float[4];
    public float bScalar = 1f;

    [NonSerialized] private Vector4 lastOut;

    static readonly string[] CompNames = { "x", "y", "z", "w" };

    static int CountOf(RadioButtonSet t) => t.IsSelected("Vec2") ? 2 : t.IsSelected("Vec4") ? 4 : 3;
    static Type VecType(int n) => n == 2 ? typeof(Vector2) : n == 4 ? typeof(Vector4) : typeof(Vector3);

    public override void DoInit()
    {
        if (bComponents == null || bComponents.Length != 4) bComponents = new float[4];
        EnsurePorts();
    }

    ValueConnectionKnob FindPort(string name)
    {
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            if (dynamicConnectionPorts[i].name == name) return (ValueConnectionKnob)dynamicConnectionPorts[i];
        }
        return null;
    }

    // Per-port healing: only the port whose type changed is rebuilt, so retyping B never
    // costs A's connections (and vice versa)
    void EnsurePorts()
    {
        int nA = CountOf(aType);
        bool scalar = bMode.IsSelected("scalar");
        Type bPortType = scalar ? typeof(float) : VecType(CountOf(bType));
        EnsurePort("a", VecType(nA), Direction.In);
        EnsurePort("b", bPortType, Direction.In);
        EnsurePort("out", VecType(nA), Direction.Out);
        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            var port = dynamicConnectionPorts[i];
            if (port.name != "a" && port.name != "b" && port.name != "out")
            {
                port.ClearConnections();
                DeleteConnectionPort(port);
            }
        }
    }

    void EnsurePort(string name, Type type, Direction dir)
    {
        var existing = FindPort(name);
        if (existing != null && existing.valueType == type) return;
        if (existing != null)
        {
            existing.ClearConnections();
            DeleteConnectionPort(existing);
        }
        CreateValueConnectionKnob(new ValueConnectionKnobAttribute(
            name, dir, type, dir == Direction.In ? NodeSide.Left : NodeSide.Right));
    }

    public override void NodeGUI()
    {
        EnsurePorts();
        // Snapshot everything that shapes GUI structure BEFORE the radios: a click mutates
        // them mid-pass and the rest of this pass must match its Layout (ports rebuild next pass)
        bool scalarNow = bMode.IsSelected("scalar");
        int nBNow = CountOf(bType);
        var aKnob = FindPort("a");
        var bKnob = FindPort("b");
        var outKnob = FindPort("out");
        bool bConnected = bKnob != null && bKnob.connected();

        GUILayout.BeginVertical();
        RadioButtonsHorizontal(aType);
        RadioButtonsHorizontal(op);
        RadioButtonsHorizontal(bMode);
        if (!scalarNow) RadioButtonsHorizontal(bType);

        GUILayout.BeginHorizontal();
        if (aKnob != null) aKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        if (outKnob != null) outKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (bKnob != null) bKnob.DisplayLayout();
        if (!bConnected)
        {
            if (scalarNow)
            {
                bScalar = RTEditorGUI.FloatField(bScalar);
            }
            else
            {
                for (int i = 0; i < nBNow; i++)
                {
                    bComponents[i] = RTEditorGUI.FloatField(bComponents[i]);
                }
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"= ({lastOut.x:0.00}, {lastOut.y:0.00}, {lastOut.z:0.00}, {lastOut.w:0.00})");
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    static Vector4 ReadVec(ValueConnectionKnob knob, int lanes)
    {
        if (knob == null || !knob.connected()) return Vector4.zero;
        if (lanes == 2) { Vector2 v = knob.GetValue<Vector2>(); return new Vector4(v.x, v.y, 0f, 0f); }
        if (lanes == 4) return knob.GetValue<Vector4>();
        Vector3 v3 = knob.GetValue<Vector3>();
        return new Vector4(v3.x, v3.y, v3.z, 0f);
    }

    float Apply(float a, float b)
    {
        if (op.IsSelected("-")) return a - b;
        if (op.IsSelected("*")) return a * b;
        if (op.IsSelected("/")) return b == 0f ? 0f : a / b; // no NaN poisoning downstream
        return a + b;
    }

    public override bool DoCalc()
    {
        var aKnob = FindPort("a");
        var bKnob = FindPort("b");
        var outKnob = FindPort("out");
        if (aKnob == null || bKnob == null || outKnob == null)
        {
            EnsurePorts();
            return true;
        }

        int nA = CountOf(aType);
        Vector4 a = ReadVec(aKnob, nA);

        bool scalar = bMode.IsSelected("scalar");
        Vector4 b;
        int nB;
        if (scalar)
        {
            float s = bKnob.connected() ? bKnob.GetValue<float>() : bScalar;
            b = new Vector4(s, s, s, s);
            nB = nA; // broadcast covers every lane of A
        }
        else
        {
            nB = CountOf(bType);
            b = bKnob.connected()
                ? ReadVec(bKnob, nB)
                : new Vector4(bComponents[0], bComponents[1], bComponents[2], bComponents[3]);
        }

        // aligned from x; A's lanes beyond B's width pass through untouched
        Vector4 result = a;
        int lanes = Mathf.Min(nA, nB);
        for (int i = 0; i < lanes; i++)
        {
            result[i] = Apply(a[i], b[i]);
        }
        lastOut = result;

        if (nA == 2) outKnob.SetValue(new Vector2(result.x, result.y));
        else if (nA == 4) outKnob.SetValue(result);
        else outKnob.SetValue(new Vector3(result.x, result.y, result.z));
        return true;
    }
}
