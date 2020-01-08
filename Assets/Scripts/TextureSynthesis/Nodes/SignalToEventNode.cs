
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/SignalToEvent")]
public class SignalToEventNode : TextureSynthNode
{
    public override string GetID => "SignalToEventNode";
    public override string Title { get { return "SignalToEvent"; } }

    public override Vector2 DefaultSize { get { return new Vector2(200, 120); } }

    [ValueConnectionKnob("inputSignal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob inputSignalKnob;

    [ValueConnectionKnob("threshold", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob thresholdKnob;

    [ValueConnectionKnob("outputEvent", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob outputEventKnob;
    public bool output;

    public RadioButtonSet triggerMode;

    public float threshold = 1;

    float lastSignalValue;

    public void Awake()
    {
        if (triggerMode == null)
        {
            triggerMode = new RadioButtonSet("leadingEdge", "trailingEdge", "high", "low");
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        RadioButtons(triggerMode);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        inputSignalKnob.DisplayLayout();
        FloatKnobOrSlider(ref threshold, 0, 1, thresholdKnob);
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label(string.Format("Trigger: {0}", output.ToString()));
        outputEventKnob.DisplayLayout();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        float signalValue = inputSignalKnob.GetValue<float>();
        if ( signalValue != lastSignalValue)
        {
            if (triggerMode.IsSelected("leadingEdge"))
            {
                output = CheckLeadingEdge(signalValue);
            } else if (triggerMode.IsSelected("trailingEdge"))
            {
                output = CheckTrailingEdge(signalValue);
            } else if (triggerMode.IsSelected("high"))
            {
                output = CheckHigh(signalValue);
            } else if (triggerMode.IsSelected("low"))
            {
                output = CheckLow(signalValue);
            }
            lastSignalValue = signalValue;
        }
        outputEventKnob.SetValue(output);
        return true;
    }

    private bool CheckLow(float signalValue)
    {
        return signalValue < threshold;
    }

    private bool CheckHigh(float signalValue)
    {
        return signalValue > threshold;
    }

    private bool CheckTrailingEdge(float signalValue)
    {
        return (signalValue < threshold) && output == false;
    }

    private bool CheckLeadingEdge(float signalValue)
    {
        return (signalValue > threshold) && output == false;
    }
}
