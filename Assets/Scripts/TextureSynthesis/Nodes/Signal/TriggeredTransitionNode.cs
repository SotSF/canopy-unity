
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/TriggeredTransition")]
public class TriggeredTransitionNode : TickingNode
{
    public override string GetID => "TriggeredTransitionNode";
    public override string Title { get { return "TriggeredTransition"; } }


    public override bool AutoLayout => true;

    private Vector2 _DefaultSize = new Vector2(220, 150);

    public override Vector2 DefaultSize => _DefaultSize;


    [ValueConnectionKnob("triggerEvent", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob triggerEventKnob;

    [ValueConnectionKnob("startValue", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob startValueKnob;
    
    [ValueConnectionKnob("endValue", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob endValueKnob;

    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;
    public float startValue = 0;
    public float endValue = 1;

    public float outValue = 0;
    public void Awake()
    {

    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        
        GUILayout.BeginVertical();
        triggerEventKnob.DisplayLayout();
        FloatKnobOrField("Start value", ref startValue, startValueKnob);
        FloatKnobOrField("End value", ref endValue, endValueKnob);

        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("Value: {0:0.0000}", outValue));
        outputSignalKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }


    public override bool Calculate()
    {
        if (!triggerEventKnob.connected())
        {
            outputSignalKnob.SetValue(startValue);
            return true;
        }
        var triggered = triggerEventKnob.GetValue<bool>();
        if (triggered) {
            outValue = endValue;
        }
        outputSignalKnob.SetValue(outValue);
        return true;
    }
}
