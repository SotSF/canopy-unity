
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/Latch")]
public class SignalLatchNode : TickingNode
{
    public override string GetID => "SignalLatchNode";
    public override string Title { get { return "SignalLatch"; } }


    public override bool AutoLayout => true;

    private Vector2 _DefaultSize = new Vector2(220, 150);

    public override Vector2 DefaultSize => _DefaultSize;


    [ValueConnectionKnob("latchControl", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob latchControlKnob;
    bool latched;

    [ValueConnectionKnob("controlSignal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob controlSignalKnob;
    [ValueConnectionKnob("sensitivity", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob sensitivityKnob;

    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;

    public bool additive = true;
    public float latchedValue;
    public bool useRange;
    public float max = 1;
    public float min = 0;

    public float sensitivity;

    public void Awake()
    {

    }


    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        
        GUILayout.BeginVertical();
        latchControlKnob.DisplayLayout();
        controlSignalKnob.DisplayLayout();
        useRange = RTEditorGUI.Toggle(useRange, "Use range");
        additive = RTEditorGUI.Toggle(additive, "Additive");
        if (useRange)
        {

            min = RTEditorGUI.FloatField("Min", min);
            max = RTEditorGUI.FloatField("Max", max);
            FloatKnobOrSlider(ref sensitivity, min, max, sensitivityKnob);
        } else
        {
            sensitivityKnob.DisplayLayout();
        }
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("Value: {0:0.0000}", latchedValue));
        outputSignalKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }


    public override bool Calculate()
    {
        if (!controlSignalKnob.connected())
        {
            outputSignalKnob.SetValue(latchedValue);
            return true;
        }
        var inputValue = controlSignalKnob.GetValue<float>();
        var sensitivity = sensitivityKnob.connected() ? sensitivityKnob.GetValue<float>() : 1;
        if (additive)
        {
            latchedValue += inputValue * sensitivity;
        } else
        {
            if (latchControlKnob.connected()){
                if (latchControlKnob.GetValue<bool>()){
                    latched = !latched;
                }
            }
            if (!latched){
                latchedValue = inputValue;
            }
        }
        if (useRange)
        {
            latchedValue = Mathf.Clamp(latchedValue, min, max);
        }
        outputSignalKnob.SetValue(latchedValue);
        return true;
    }
}
