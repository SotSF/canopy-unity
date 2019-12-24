using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using Oscillators;
using SecretFire.TextureSynth;
using UnityEngine;


[Node(false, "Inputs/Oscillator")]
public class PeriodicSignalNode : TickingNode
{
    public const string ID = "periodicSignal";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "PeriodicSignal"; } }
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
    public float period, amplitude, phase;

    private void Awake()
    {
        if (period == 0) period = 2;
        oscParams = new Oscillator(period, amplitude, phase);
        if (Application.isPlaying)
            OscillatorManager.instance.Register(this);
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        //Input pins & internal controls
        GUILayout.BeginVertical();
        //periodInputKnob.DisplayLayout();
        //if (!periodInputKnob.connected())
        //{
        //    period = RTEditorGUI.Slider(period, 0.01f, 50);
        //}
        KnobOrSlider(ref period, 0.01f, 50, periodInputKnob);
        amplInputKnob.DisplayLayout();
        if (!amplInputKnob.connected())
        {
            amplitude = RTEditorGUI.FloatField(amplitude);
        }
        //phaseInputKnob.DisplayLayout();
        //if (!phaseInputKnob.connected())
        //{
        //    phase = RTEditorGUI.Slider(phase, 0, 2 * Mathf.PI);
        //}
        KnobOrSlider(ref phase, 0, 2 * Mathf.PI, phaseInputKnob);
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
            if (Application.isPlaying)
            {
                oscParams = new Oscillator(period, amplitude, phase);
                OscillatorManager.instance.Register(this);
            }
        }
        float value;
        if (Application.isPlaying)
        {
            value = OscillatorManager.instance.GetValue(this);
        }
        else
        {
            value = Mathf.Sin((2 * Mathf.PI - phase) / period) * amplitude;
        }
        outputKnob.SetValue(value);
        return true;
    }
}