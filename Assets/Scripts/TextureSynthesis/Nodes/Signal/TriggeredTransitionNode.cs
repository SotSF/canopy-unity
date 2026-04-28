
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/TriggeredTransition")]
public class TriggeredTransitionNode : SignalNode
{
    public override string GetID => "TriggeredTransitionNode";
    public override string Title { get { return "TriggeredTransition"; } }


    public override bool AutoLayout => true;

    private Vector2 _DefaultSize = new Vector2(220, 150);

    protected override Vector2 BaseDefaultSize => _DefaultSize;


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

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = outputSignalKnob,
            getValue   = () => outputSignalKnob.GetValue<float>(),
            label      = "Output",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        triggerEventKnob.DisplayLayout();
        FloatKnobOrField("Start value", ref startValue, startValueKnob);
        FloatKnobOrField("End value", ref endValue, endValueKnob);

        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("Value: {0:0.0000}", outValue));
        GUILayout.EndHorizontal();

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }


    public override bool DoCalc()
    {
        if (startValueKnob.connected())
        {
            startValue = startValueKnob.GetValue<float>();
        }
        if (endValueKnob.connected())
        {
            endValue = endValueKnob.GetValue<float>();
        }
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
