
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/SpectrumVisualizer")]
public class SpectrumVisualizerNode : TickingNode
{
    public override string GetID => "SpectrumVisualizerNode";
    public override string Title { get { return "SpectrumVisualizer"; } }
    private Vector2 _DefaultSize = new Vector2(110, 110); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.In, typeof(float[]), NodeSide.Left)]
    public ValueConnectionKnob spectrumDataKnob;
    private float[] spectrumData;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(75,96);
    public RenderTexture outputTex;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/SpectrumVisualizerFilter");
        patternKernel = patternShader.FindKernel("PatternKernel");
        InitializeRenderTexture();
    }
    private void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }
    
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        spectrumDataKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.Space(4);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(DefaultSize.x-20);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        var dummySpectrum = new float[32];
        
        for (int i = 0; i < 32; i++)
        {
            dummySpectrum[i] = 0.5f;
        }

        spectrumData = spectrumDataKnob.GetValue<float[]>();
        if (spectrumData != null)
        {
            patternShader.SetInt("width", outputSize.x);
            patternShader.SetInt("height", outputSize.y);
            patternShader.SetInt("spectrumSize", spectrumData.Length);
            patternShader.SetFloats("spectrumData", spectrumData);
            patternShader.SetTexture(patternKernel, "outputTex", outputTex);
            uint tx, ty, tz;
            patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
            var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
            var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
            patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
            outputTexKnob.SetValue(outputTex);
        }
        return true;
    }
}
