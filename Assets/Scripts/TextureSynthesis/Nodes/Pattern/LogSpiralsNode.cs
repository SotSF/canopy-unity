
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/LogSpirals")]
public class LogSpiralsNode : TickingNode
{
    public override string GetID => "LogSpiralsNode";
    public override string Title { get { return "LogSpirals"; } }
    private Vector2 _DefaultSize = new Vector2(250, 500);

    public override Vector2 DefaultSize => _DefaultSize;
    [ValueConnectionKnob("globalTimeFactor", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob globalTimeFactorKnob;
    private float globalTimeFactor = 1.1f;

    [ValueConnectionKnob("spikeMotionTimeScalingFactor", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob spikeMotionTimeScalingFactorKnob;
    public float spikeMotionTimeScalingFactor = -0.4f;

    [ValueConnectionKnob("repetitionsPerSpiralTurn", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob repetitionsPerSpiralTurnKnob;
    public float repetitionsPerSpiralTurn = 14;

    [ValueConnectionKnob("primaryOscPeriod", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob primaryOscPeriodKnob;
    public float primaryOscPeriod = 15;

    [ValueConnectionKnob("distCutoff", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob distCutoffKnob;
    public float distCutoff = 1.5f;

    [ValueConnectionKnob("colorRangeStart", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob colorRangeStartKnob;
    public float colorRangeStart = 0.55f;

    [ValueConnectionKnob("colorRangeWidth", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob colorRangeWidthKnob;
    public float colorRangeWidth = 0.35f;

    [ValueConnectionKnob("waveOffset", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob waveOffsetKnob;
    public float waveOffset = 0.0172f;

    [ValueConnectionKnob("baseAmplitude", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob baseAmplitudeKnob;
    public float baseAmplitude = 0.04f;

    [ValueConnectionKnob("spiralGrowthFactor", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob spiralGrowthFactorKnob;
    public float spiralGrowthFactor = 0.1f;

    [ValueConnectionKnob("spiralTightness", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob spiralTightnessKnob;
    public float spiralTightness = 0.35f;

    [ValueConnectionKnob("colorIterations", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob colorIterationsKnob;
    public int colorIterations = 32;

    [ValueConnectionKnob("spiralTightness", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob spiralCountKnob;
    public int spiralCount = 4;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    public RenderTexture outputTex;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/LogSpiralsPattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
        InitializeRenderTexture();
    }
    private void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputSize = new Vector2Int(256, 256);
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }
    
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref globalTimeFactor, -2, 2, globalTimeFactorKnob);
        FloatKnobOrSlider(ref spikeMotionTimeScalingFactor, -2, 2, spikeMotionTimeScalingFactorKnob);
        FloatKnobOrSlider(ref repetitionsPerSpiralTurn, 0, 64, repetitionsPerSpiralTurnKnob);
        FloatKnobOrSlider(ref primaryOscPeriod, 0.1f, 60, primaryOscPeriodKnob);
        FloatKnobOrSlider(ref distCutoff, 0, 5, distCutoffKnob);
        FloatKnobOrSlider(ref colorRangeStart, 0, 1, colorRangeStartKnob);
        FloatKnobOrSlider(ref colorRangeWidth, 0, 1, colorRangeWidthKnob);
        FloatKnobOrSlider(ref waveOffset, 0, 0.2f, waveOffsetKnob);
        FloatKnobOrSlider(ref baseAmplitude, 0, 0.1f, baseAmplitudeKnob);
        FloatKnobOrSlider(ref spiralGrowthFactor, 0, 1, spiralGrowthFactorKnob);
        FloatKnobOrSlider(ref spiralTightness, 0, 1, spiralTightnessKnob);
        IntKnobOrSlider(ref colorIterations, 1, 48, colorIterationsKnob);
        IntKnobOrSlider(ref spiralCount, 1, 8, spiralCountKnob);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.DisplayLayout();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        globalTimeFactor = globalTimeFactorKnob.connected() ? globalTimeFactorKnob.GetValue<float>() : globalTimeFactor;
        patternShader.SetFloat("globalTimeFactor", globalTimeFactor);
        patternShader.SetFloat("time", Time.time);
        spikeMotionTimeScalingFactor = spikeMotionTimeScalingFactorKnob.connected() ? spikeMotionTimeScalingFactorKnob.GetValue<float>() : spikeMotionTimeScalingFactor;
        patternShader.SetFloat("spikeMotionTimeScalingFactor", spikeMotionTimeScalingFactor);
        repetitionsPerSpiralTurn = repetitionsPerSpiralTurnKnob.connected() ? repetitionsPerSpiralTurnKnob.GetValue<float>() : repetitionsPerSpiralTurn;
        patternShader.SetFloat("repetitionsPerSpiralTurn", repetitionsPerSpiralTurn);
        primaryOscPeriod = primaryOscPeriodKnob.connected() ? primaryOscPeriodKnob.GetValue<float>() : primaryOscPeriod;
        patternShader.SetFloat("primaryOscPeriod", primaryOscPeriod);
        distCutoff = distCutoffKnob.connected() ? distCutoffKnob.GetValue<float>() : distCutoff;
        patternShader.SetFloat("distCutoff", distCutoff);
        colorRangeStart = colorRangeStartKnob.connected() ? colorRangeStartKnob.GetValue<float>() : colorRangeStart;
        patternShader.SetFloat("colorRangeStart", colorRangeStart);
        colorRangeWidth = colorRangeWidthKnob.connected() ? colorRangeWidthKnob.GetValue<float>() : colorRangeWidth;
        patternShader.SetFloat("colorRangeWidth", colorRangeWidth);
        waveOffset = waveOffsetKnob.connected() ? waveOffsetKnob.GetValue<float>() : waveOffset;
        patternShader.SetFloat("waveOffset", waveOffset);
        baseAmplitude = baseAmplitudeKnob.connected() ? baseAmplitudeKnob.GetValue<float>() : baseAmplitude;
        patternShader.SetFloat("baseAmplitude", baseAmplitude);
        spiralGrowthFactor = spiralGrowthFactorKnob.connected() ? spiralGrowthFactorKnob.GetValue<float>() : spiralGrowthFactor;
        patternShader.SetFloat("spiralGrowthFactor", spiralGrowthFactor);
        spiralTightness = spiralTightnessKnob.connected() ? spiralTightnessKnob.GetValue<float>() : spiralTightness;
        patternShader.SetFloat("spiralTightness", spiralTightness);

        patternShader.SetInt("spiralCount", spiralCount);
        patternShader.SetInt("colorIterations", colorIterations);

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);

        patternShader.SetTexture(patternKernel, "outputTex", outputTex);

        uint tx,ty,tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
        outputTexKnob.SetValue(outputTex);
        return true;
    }
}
