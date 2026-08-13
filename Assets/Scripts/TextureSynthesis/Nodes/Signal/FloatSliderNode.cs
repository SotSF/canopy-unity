using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A scrubbable float value: slider position (0..1) is lerped between min and max,
/// so retuning the range preserves the slider position. Min/max are editable fields
/// or drivable input ports.
/// </summary>
[Node(false, "Signal/FloatSlider")]
public class FloatSliderNode : SignalNode
{
    public override string GetID => "FloatSliderNode";
    public override string Title { get { return "FloatSlider"; } }

    protected override Vector2 BaseDefaultSize => new Vector2(200, 90);

    [ValueConnectionKnob("min", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob minKnob;

    [ValueConnectionKnob("max", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob maxKnob;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    public float sliderPosition = 0.5f;
    public float rescaleMin = 0;
    public float rescaleMax = 1;

    private float Value => Mathf.Lerp(rescaleMin, rescaleMax, sliderPosition);

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = valueKnob,
            getValue   = () => Value,
            label      = "value",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        FloatKnobOrField(GUIContent.none, ref rescaleMin, minKnob);
        FloatKnobOrField(GUIContent.none, ref rescaleMax, maxKnob);

        // Scrub in real units; store the normalized position
        float displayValue = Value;
        float newValue = RTEditorGUI.Slider(displayValue, rescaleMin, rescaleMax);
        if (!Mathf.Approximately(newValue, displayValue))
        {
            sliderPosition = Mathf.InverseLerp(rescaleMin, rescaleMax, newValue);
        }

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (minKnob != null && minKnob.connected())
        {
            rescaleMin = minKnob.GetValue<float>();
        }
        if (maxKnob != null && maxKnob.connected())
        {
            rescaleMax = maxKnob.GetValue<float>();
        }
        valueKnob.SetValue(Value);
        return true;
    }
}
