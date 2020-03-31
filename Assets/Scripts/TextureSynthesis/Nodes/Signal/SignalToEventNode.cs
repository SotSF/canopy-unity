
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/SignalToEvent")]
public class SignalToEventNode : TickingNode
{
    public override string GetID => "SignalToEventNode";
    public override string Title { get { return "SignalToEvent"; } }

    public override Vector2 DefaultSize { get { return new Vector2(230, 150); } }

    [ValueConnectionKnob("inputSignal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob inputSignalKnob;

    [ValueConnectionKnob("threshold", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob thresholdKnob;

    [ValueConnectionKnob("outputEvent", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob outputEventKnob;
    public bool output;

    public RadioButtonSet triggerMode;

    public float threshold = 1;

    bool wasOverThreshold;
    float signalValue;

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
        signalValue = inputSignalKnob.GetValue<float>();
        switch (triggerMode.SelectedOption())
        {
            case "leadingEdge":
                output = (signalValue > threshold) && !wasOverThreshold;
                break;
            case "trailingEdge":
                output = (signalValue < threshold) && wasOverThreshold;
                break;
            case "high":
                output = signalValue > threshold;
                break;
            case "low":
                output = signalValue < threshold;
                break;
            default:
                output = false;
                break;
        }
        wasOverThreshold = signalValue > threshold;
        outputEventKnob.SetValue(output);
        return true;
    }
}
