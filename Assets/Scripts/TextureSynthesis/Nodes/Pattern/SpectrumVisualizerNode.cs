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

    [ValueConnectionKnob("gradient", Direction.In, typeof(Gradient), NodeSide.Left)]
    public ValueConnectionKnob gradientKnob;
    // Rebaked every frame while a gradient is connected: the manager's gradients are
    // edited live in the inspector, and the reference doesn't change when content does
    [System.NonSerialized] private int lastGradientBakeFrame = -1;
    [System.NonSerialized] private Color[] gradientBakeBuffer;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    public Gradient barGradient;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(256,256);
    private RenderTexture outputTex;
    private Texture2D gradientTex;

    public int width = 256;
    public int height = 256;

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
    
    private void UpdateGradientTexture() => BakeGradient(barGradient);

    private void BakeGradient(Gradient gradient)
    {
        if (gradientTex == null)
        {
            gradientTex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            gradientTex.wrapMode = TextureWrapMode.Clamp;
            gradientTex.filterMode = FilterMode.Bilinear;
        }

        if (gradient != null)
        {
            if (gradientBakeBuffer == null || gradientBakeBuffer.Length != 256)
            {
                gradientBakeBuffer = new Color[256];
            }
            for (int i = 0; i < 256; i++)
            {
                gradientBakeBuffer[i] = gradient.Evaluate((float)i / 255f);
            }
            gradientTex.SetPixels(gradientBakeBuffer);
            gradientTex.Apply();
        }
    }
    
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        spectrumDataKnob.DisplayLayout();
        gradientKnob.DisplayLayout();

        #if UNITY_EDITOR
        // The connected port overrides this local gradient; keep the field editable
        // only when it's actually in effect
        if (gradientKnob == null || !gradientKnob.connected())
        {
            EditorGUI.BeginChangeCheck();
            barGradient = EditorGUILayout.GradientField(new GUIContent("Gradient"), barGradient);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateGradientTexture();
            }
        }
        #endif
        width = RTEditorGUI.IntField("Width", width );
        GUILayout.FlexibleSpace();
        height = RTEditorGUI.IntField("Height", height);
        if (width != outputSize.x || height != outputSize.y)
        {
            outputSize.x = width;
            outputSize.y = height;
            InitializeRenderTexture();
        }
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
        // Port-supplied gradient takes precedence over the node's own barGradient.
        // Once-per-frame rebake (Calculate can re-run in-frame via OnNodeChange).
        if (gradientKnob != null && gradientKnob.connected() && Time.frameCount != lastGradientBakeFrame)
        {
            var portGradient = gradientKnob.GetValue<Gradient>();
            if (portGradient != null)
            {
                lastGradientBakeFrame = Time.frameCount;
                BakeGradient(portGradient);
            }
        }
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