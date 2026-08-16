using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exponentially smooths the input signal: output chases input with time constant tau
/// (seconds). tau=0 passes through; larger tau = slower, smoother response.
/// </summary>
[Node(false, "Signal/SmoothFilter")]
public class SmoothFilterNode : SignalNode
{
    public override string GetID => "SmoothFilterNode";
    public override string Title { get { return "SmoothFilter"; } }

    protected override Vector2 BaseDefaultSize => new Vector2(200, 120);

    [ValueConnectionKnob("input", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob inputKnob;

    [ValueConnectionKnob("tau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob tauKnob;
    public float tau = 0.04f;


    [ValueConnectionKnob("output", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    
    public float value = 0;
    public float input = 0;

    private float Value => value;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = valueKnob,
            getValue   = () => Value,
            label      = "value",
        };
    }

    private float ComputeSmoothed(float x, float dt)
    {
        // guard dt=0: with tau=0 it would produce 0/0 = NaN and poison the filter permanently
        dt = Mathf.Max(dt, 1e-5f);
        var alpha = 1 / (1+tau/dt);
        return value + alpha * (x-value);
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        inputKnob.DisplayLayout();
        FloatKnobOrSlider( ref tau, 0, 0.5f, tauKnob);

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        // read tau here, not just in NodeGUI: a connected tau must keep updating
        // even when the canvas GUI is hidden/minimized
        if (tauKnob != null && tauKnob.connected())
        {
            tau = tauKnob.GetValue<float>();
        }
        if (inputKnob != null && inputKnob.connected())
        {
            input = inputKnob.GetValue<float>();
            value = ComputeSmoothed(input, Time.deltaTime);
        }

        valueKnob.SetValue(value);
        return true;
    }
}
