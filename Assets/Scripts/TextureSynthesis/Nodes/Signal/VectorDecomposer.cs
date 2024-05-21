
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/VectorDecomposer")]
public class VectorDecomposerNode : Node
{
    public override string GetID => "VectorDecomposerNode";
    public override string Title { get { return "VectorDecomposer"; } }


    public override bool AutoLayout => true;

    private Vector2 _DefaultSize = new Vector2(180, 150); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Input vector", Direction.In, typeof(Vector2), NodeSide.Left)]
    public ValueConnectionKnob inputVectorKnob;

    [ValueConnectionKnob("outX", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob xOutputKnob;

    [ValueConnectionKnob("outY", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob yOutputKnob;

    public float xValue;
    public float yValue;

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        
        GUILayout.BeginVertical();
        inputVectorKnob.DisplayLayout();
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("x: {0:0.0000}", xValue));
        xOutputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("y: {0:0.0000}", yValue));
        yOutputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }


    public override bool Calculate()
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
