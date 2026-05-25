
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/Derivative")]
public class SignalDerivativeNode : SignalNode
{
    public override string GetID => "SignalDerivativeNode";
    public override string Title { get { return "SignalDerivative"; } }

    public override bool AutoLayout => true;

    private Vector2 _DefaultSize = new Vector2(220, 150);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("inputSignal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob inputSignalKnob;

    [ValueConnectionKnob("attackTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob attackTauKnob;

    [ValueConnectionKnob("releaseTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob releaseTauKnob;

    [ValueConnectionKnob("derivative", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob derivativeKnob;

    public float attackTau  = 0.04f;
    public float releaseTau = 0.15f;

    [System.NonSerialized] float lastInput;
    [System.NonSerialized] bool  hasLast;
    public float derivative;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = derivativeKnob,
            getValue   = () => derivativeKnob.GetValue<float>(),
            label      = "d/dt",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        inputSignalKnob.DisplayLayout();
        FloatKnobOrSlider(ref attackTau,  0f, 1f, attackTauKnob);
        FloatKnobOrSlider(ref releaseTau, 0f, 2f, releaseTauKnob);
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("d/dt: {0:0.0000}", derivative));
        GUILayout.EndHorizontal();

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (attackTauKnob.connected())  attackTau  = attackTauKnob.GetValue<float>();
        if (releaseTauKnob.connected()) releaseTau = releaseTauKnob.GetValue<float>();

        if (!inputSignalKnob.connected())
        {
            hasLast = false;
            derivative = 0f;
            derivativeKnob.SetValue(derivative);
            return true;
        }

        var input = inputSignalKnob.GetValue<float>();
        var dt = Time.deltaTime;

        if (!hasLast || dt <= 0f)
        {
            lastInput = input;
            hasLast = true;
            derivativeKnob.SetValue(derivative);
            return true;
        }

        var raw = (input - lastInput) / dt;
        lastInput = input;

        // Asymmetric exponential smoothing: alpha = 1 - exp(-dt/tau) is the
        // frame-rate-independent EMA coefficient; tau is the time to reach
        // ~63% of a step. tau == 0 disables smoothing on that side.
        float tau = raw > derivative ? attackTau : releaseTau;
        float alpha = tau > 0f ? 1f - Mathf.Exp(-dt / tau) : 1f;
        derivative += alpha * (raw - derivative);

        derivativeKnob.SetValue(derivative);
        return true;
    }
}
