using NodeEditorFramework;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bounded leaky integrator: accumulates its input (scaled, per-second) between explicit
/// min/max bounds while exponentially decaying toward a setpoint. The drive is scaled by
/// remaining headroom — positive input asymptotically approaches max, negative input
/// approaches min — so the bounds are soft ceilings the signal saturates into, never
/// hard clips mid-motion. decay = 0 accumulates monotonically toward the bound; with no
/// input it's a classic exponential approach to the setpoint from either side. Fed
/// envelope pulses it behaves like heat: each press adds energy, holds keep adding,
/// everything cools back toward the setpoint.
/// </summary>
[Node(false, "Signal/Accumulator")]
public class AccumulatorNode : SignalNode
{
    public override string GetID => "AccumulatorNode";
    public override string Title { get { return "Accumulator"; } }

    private Vector2 _DefaultSize = new Vector2(190, 130);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("input", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob inputKnob;

    [ValueConnectionKnob("setpoint", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob setpointKnob;

    [ValueConnectionKnob("decay", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob decayKnob;

    [ValueConnectionKnob("scale", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob scaleKnob;

    [ValueConnectionKnob("min", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob minKnob;

    [ValueConnectionKnob("max", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob maxKnob;

    [ValueConnectionKnob("out", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputKnob;

    public float setpoint = 0f;
    public float decayRate = 1f;
    public float inputScale = 1f;
    public float minValue = 0f;
    public float maxValue = 1f;

    [System.NonSerialized] private float value;
    [System.NonSerialized] private bool valueInitialized;
    [System.NonSerialized] private int lastCalcFrame = -1;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = outputKnob,
            getValue   = () => value,
            label      = "out",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        inputKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset"))
        {
            value = Mathf.Clamp(setpoint, minValue, maxValue);
            valueInitialized = true;
        }
        GUILayout.EndHorizontal();
        FloatKnobOrField("setpoint", ref setpoint, setpointKnob);
        FloatKnobOrField("decay", ref decayRate, decayKnob);
        FloatKnobOrField("scale", ref inputScale, scaleKnob);
        FloatKnobOrField("min", ref minValue, minKnob);
        FloatKnobOrField("max", ref maxValue, maxKnob);

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (setpointKnob != null && setpointKnob.connected()) setpoint = setpointKnob.GetValue<float>();
        if (decayKnob != null && decayKnob.connected()) decayRate = decayKnob.GetValue<float>();
        if (scaleKnob != null && scaleKnob.connected()) inputScale = scaleKnob.GetValue<float>();
        if (minKnob != null && minKnob.connected()) minValue = minKnob.GetValue<float>();
        if (maxKnob != null && maxKnob.connected()) maxValue = maxKnob.GetValue<float>();

        if (!valueInitialized)
        {
            value = Mathf.Clamp(setpoint, minValue, maxValue);
            valueInitialized = true;
        }

        // OnNodeChange re-runs Calculate in-frame; integrate only on the first pass
        if (Time.frameCount != lastCalcFrame)
        {
            lastCalcFrame = Time.frameCount;
            float dt = Time.deltaTime;

            // Headroom-scaled drive: at full headroom the input integrates at input*scale
            // per second, tapering to zero as the value nears the approached bound — an
            // exponential saturation into [min, max] rather than a hard clip.
            float range = maxValue - minValue;
            float input = (inputKnob != null && inputKnob.connected()) ? inputKnob.GetValue<float>() : 0f;
            if (range > 1e-5f && !float.IsNaN(input) && !float.IsInfinity(input))
            {
                float drive = input * inputScale * dt;
                if (drive > 0f) value += drive * (maxValue - value) / range;
                else value += drive * (value - minValue) / range;
            }

            // Exact exponential decay for any dt. Negative decay (unbounded growth)
            // explodes too easily to be useful, so it's clamped to hold instead.
            float k = Mathf.Max(decayRate, 0f);
            if (k > 0f)
            {
                value = setpoint + (value - setpoint) * Mathf.Exp(-k * dt);
            }

            // Decay toward an out-of-range setpoint pins at the nearer bound
            value = Mathf.Clamp(value, minValue, maxValue);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = Mathf.Clamp(setpoint, minValue, maxValue);
        }

        if (outputKnob != null) outputKnob.SetValue(value);
        return true;
    }
}
