using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Node(false, "Pattern/SpectrumVisualizer")]
public class SpectrumVisualizerNode : TickingNode
{
    public override string GetID => "SpectrumVisualizerNode";
    public override string Title { get { return "SpectrumVisualizer"; } }
    private Vector2 _DefaultSize = new Vector2(150, 200); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.In, typeof(float[]), NodeSide.Left)]
    public ValueConnectionKnob spectrumDataKnob;
    private float[] spectrumData;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    public Gradient barGradient;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(128,128);
    public RenderTexture outputTex;
    private Texture2D gradientTex;

    public override void DoInit()
    {
        patternShader = Resources.Load<ComputeShader>("NodeShaders/SpectrumVisualizerFilter");
        patternKernel = patternShader.FindKernel("PatternKernel");
        InitializeRenderTexture();
        
        if (barGradient == null)
        {
            barGradient = new Gradient();
            barGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.cyan, 0.0f), new GradientColorKey(Color.blue, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
            );
        }
        UpdateGradientTexture();
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
    
    private void UpdateGradientTexture()
    {
        if (gradientTex == null)
        {
            gradientTex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            gradientTex.wrapMode = TextureWrapMode.Clamp;
            gradientTex.filterMode = FilterMode.Bilinear;
        }
        
        if (barGradient != null)
        {
            Color[] pixels = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                pixels[i] = barGradient.Evaluate((float)i / 255f);
            }
            gradientTex.SetPixels(pixels);
            gradientTex.Apply();
        }
    }
    
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        spectrumDataKnob.DisplayLayout();

        #if UNITY_EDITOR
        EditorGUI.BeginChangeCheck();
        barGradient = EditorGUILayout.GradientField(new GUIContent("Gradient"), barGradient);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateGradientTexture();
        }
        #endif

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

    public override bool DoCalc()
    {
        spectrumData = spectrumDataKnob.GetValue<float[]>();
        if (spectrumData != null)
        {
            if (gradientTex == null) UpdateGradientTexture();

            patternShader.SetInt("width", outputSize.x);
            patternShader.SetInt("height", outputSize.y);
            patternShader.SetInt("spectrumSize", spectrumData.Length);
            patternShader.SetFloats("spectrumData", spectrumData);
            patternShader.SetTexture(patternKernel, "outputTex", outputTex);
            patternShader.SetTexture(patternKernel, "gradientTex", gradientTex);
            
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