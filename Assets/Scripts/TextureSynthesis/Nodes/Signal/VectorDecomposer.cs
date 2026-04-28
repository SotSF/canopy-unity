
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/VectorDecomposer")]
public class VectorDecomposerNode : SignalNode
{
    public override string GetID => "VectorDecomposerNode";
    public override string Title { get { return "VectorDecomposer"; } }


    public override bool AutoLayout => true;

    private Vector2 _DefaultSize = new Vector2(180, 150);

    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("Input vector", Direction.In, typeof(Vector2), NodeSide.Left)]
    public ValueConnectionKnob inputVectorKnob;

    [ValueConnectionKnob("outX", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob xOutputKnob;

    [ValueConnectionKnob("outY", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob yOutputKnob;

    public float xValue;
    public float yValue;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = xOutputKnob,
            getValue   = () => xOutputKnob.GetValue<float>(),
            label      = "X",
        };
        yield return new SignalChannel
        {
            outputKnob = yOutputKnob,
            getValue   = () => yOutputKnob.GetValue<float>(),
            label      = "Y",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        inputVectorKnob.DisplayLayout();
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        GUILayout.Label(string.Format("x: {0:0.0000}", xValue));
        GUILayout.Label(string.Format("y: {0:0.0000}", yValue));
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }


    public override bool DoCalc()
    {
        if (!inputVectorKnob.connected())
        {
            xOutputKnob.ResetValue();
            yOutputKnob.ResetValue();
            return true;
        }
        var inVector = inputVectorKnob.GetValue<Vector2>();
        xValue = inVector.x;
        yValue = inVector.y;
        xOutputKnob.SetValue(xValue);
        yOutputKnob.SetValue(yValue);
        return true;
    }
}
