using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using Oscillators;
using UnityEngine;


[Node(false, "Inputs/Oscillator")]
public class OscillatorNode : Node
{
    public const string ID = "oscillatorNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Oscillator"; } }
    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    [ValueConnectionKnob("Frequency", Direction.In, typeof(float))]
    public ValueConnectionKnob freqInputKnob;

    [ValueConnectionKnob("Amplitude", Direction.In, typeof(float))]
    public ValueConnectionKnob amplInputKnob;

    [ValueConnectionKnob("Phase", Direction.In, typeof(float))]
    public ValueConnectionKnob phaseInputKnob;

    [ValueConnectionKnob("Output", Direction.Out, typeof(float))]
    public ValueConnectionKnob outputKnob;

    public Oscillator oscParams;

    private void Awake()
    {
        oscParams = new Oscillator(0.5f,1,0);
        OscillatorManager.instance.Register(this);
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        freqInputKnob.DisplayLayout();
        amplInputKnob.DisplayLayout();
        phaseInputKnob.DisplayLayout();
        GUILayout.EndVertical();

        outputKnob.DisplayLayout();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        var newFreq = freqInputKnob.GetValue<float>();
        var newAmpl = amplInputKnob.GetValue<float>();
        newAmpl = newAmpl != 0 ? newAmpl : 1;
        var newPhase = phaseInputKnob.GetValue<float>();
        if (newFreq != oscParams.frequency || 
            newAmpl != oscParams.amplitude || 
            newPhase != oscParams.phase)
        {
            if (newFreq != oscParams.frequency)
            {
                //Find a phase such that the oscillator won't instantaneously jump in amplitude
                //If phase and freq change on the same Calculate()... we pick freq.
                float t = Time.time;
                newPhase = 2 * Mathf.PI * t - (oscParams.frequency /newFreq) * (2 * Mathf.PI * t - oscParams.phase);
            }
            oscParams = new Oscillator(newFreq, newAmpl, newPhase);
            OscillatorManager.instance.Register(this);
        }
        float value = OscillatorManager.instance.GetValue(this);
        outputKnob.SetValue(value);
        return true;
    }
}