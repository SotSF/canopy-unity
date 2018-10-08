using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;
using Oscillators;

[Node(false, "Pattern/AnalogSynth")]
public class AnalogSynthNode: Node
{
    public const string ID = "synthNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "AnalogSynth"; } }
    public override Vector2 DefaultSize { get { return new Vector2(220, 150); } }

    [ValueConnectionKnob("Period", Direction.In, typeof(float))]
    public ValueConnectionKnob periodInputKnob;

    [ValueConnectionKnob("Phase", Direction.In, typeof(float))]
    public ValueConnectionKnob phaseInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    public ValueConnectionKnob textureOutputKnob;

    public float period, amplitude, phase;

    public RenderTexture outputTex;

    private Vector2Int outputSize = new Vector2Int(256,256);

    private ComputeShader synthShader;
    private int kernelId;

    private void Awake()
    {
        synthShader = Resources.Load<ComputeShader>("Patterns/PatternShaders/SynthPattern");
        kernelId = synthShader.FindKernel("CSMain");
        InitializeRenderTexture();
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        periodInputKnob.DisplayLayout();
        if (!periodInputKnob.connected())
        {
            period = RTEditorGUI.Slider(period, 0.01f, 50);
        }
        phaseInputKnob.DisplayLayout();
        if (!phaseInputKnob.connected())
        {
            phase = RTEditorGUI.Slider(phase, 0, 2 * Mathf.PI);
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        var newPeriod = periodInputKnob.connected() ? periodInputKnob.GetValue<float>() : period;
        var newPhase = phaseInputKnob.connected() ? phaseInputKnob.GetValue<float>() : phase;
        if (newPeriod != period ||
            newPhase != phase)
        {
            if (newPeriod != period)
            {
                //Find a phase such that the oscillator won't instantaneously jump in amplitude
                //If phase and freq change on the same Calculate()... we pick freq.
                float t = Time.time;
                newPhase = 2 * Mathf.PI * t - (newPeriod / period) * (2 * Mathf.PI * t - phase);
            }
            period = newPeriod;
            phase = newPhase;
        }
        synthShader.SetInt("width", outputSize.x);
        synthShader.SetInt("height", outputSize.y);
        synthShader.SetFloat("period", period);
        synthShader.SetFloat("phase", phase);
        synthShader.SetFloat("time", Time.time);
        synthShader.SetTexture(kernelId, "OutputTex", outputTex);
        var threadGroupX = Mathf.CeilToInt(outputSize.x / 16.0f);
        var threadGroupY = Mathf.CeilToInt(outputSize.y / 16.0f);
        synthShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}