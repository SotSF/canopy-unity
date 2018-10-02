using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using Oscillators;
using UnityEngine;


[Node(false, "Inputs/Oscillator")]
public class OscillatorNode : Node
{
    public const string ID = "oscillatorNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Oscillator"; } }
    public override Vector2 DefaultSize { get { return new Vector2(220, 150); } }

    [ValueConnectionKnob("Period", Direction.In, typeof(float))]
    public ValueConnectionKnob periodInputKnob;

    [ValueConnectionKnob("Amplitude", Direction.In, typeof(float))]
    public ValueConnectionKnob amplInputKnob;

    [ValueConnectionKnob("Phase", Direction.In, typeof(float))]
    public ValueConnectionKnob phaseInputKnob;

    [ValueConnectionKnob("Output", Direction.Out, typeof(float))]
    public ValueConnectionKnob outputKnob;

    public Oscillator oscParams;
    private float period, amplitude, phase;

    private void Awake()
    {
        period = 2; amplitude = 1; phase = 0;
        oscParams = new Oscillator(period, amplitude, phase);
        OscillatorManager.instance.Register(this);
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        //Input pins & internal controls
        GUILayout.BeginVertical();
        periodInputKnob.DisplayLayout();
        if (!periodInputKnob.connected())
        {
            period = RTEditorGUI.Slider(period, 0.01f, 50);
        }
        amplInputKnob.DisplayLayout();
        if (!amplInputKnob.connected())
        {
            amplitude = RTEditorGUI.FloatField(amplitude);
        }
        phaseInputKnob.DisplayLayout();
        if (!phaseInputKnob.connected())
        {
            phase = RTEditorGUI.Slider(phase, 0, 2 * Mathf.PI);
        }
        GUILayout.EndVertical();

        //Output pin
        outputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        var newPeriod = periodInputKnob.connected() ? periodInputKnob.GetValue<float>() : period;
        var newAmpl   = amplInputKnob.connected()   ? amplInputKnob.GetValue<float>()   : amplitude;
        var newPhase  = phaseInputKnob.connected()  ? phaseInputKnob.GetValue<float>()  : phase;
        if (newPeriod != oscParams.period || 
            newAmpl   != oscParams.amplitude || 
            newPhase  != oscParams.phase)
        {
            if (newPeriod != oscParams.period)
            {
                //Find a phase such that the oscillator won't instantaneously jump in amplitude
                //If phase and freq change on the same Calculate()... we pick freq.
                float t = Time.time;
                newPhase = 2 * Mathf.PI * t - (newPeriod / oscParams.period) * (2 * Mathf.PI * t - oscParams.phase);
            }
            period = newPeriod;
            amplitude = newAmpl;
            phase = newPhase;
            oscParams = new Oscillator(period, amplitude, phase);
            OscillatorManager.instance.Register(this);
        }
        float value = OscillatorManager.instance.GetValue(this);
        outputKnob.SetValue(value);
        return true;
    }
}