
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/Fractal")]
public class FractalNode : TickingNode
{
    public override string GetID => "FractalNode";
    public override string Title { get { return "Fractal"; } }
    private Vector2 _DefaultSize = new Vector2(200, 200);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("maxIterations", Direction.In, typeof(int), NodeSide.Left)]
    public ValueConnectionKnob maxIterationsKnob;
    public int maxIterations;

    [ValueConnectionKnob("order", Direction.In, typeof(int), NodeSide.Left)]
    public ValueConnectionKnob orderKnob;
    public int order;

    [ValueConnectionKnob("bias", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob biasKnob;
    public float bias;

    [ValueConnectionKnob("radius", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob radiusKnob;
    public float radius;

    [ValueConnectionKnob("zoom", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob zoomKnob;
    public float zoom;

    [ValueConnectionKnob("offset", Direction.In, typeof(Vector2), NodeSide.Left)]
    public ValueConnectionKnob offsetKnob;
    public Vector2 offset = new Vector2(.5f, .5f);

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(2048,2048);
    public RenderTexture outputTex;

    public override void DoInit()
    { 
        patternShader = Resources.Load<ComputeShader>("NodeShaders/FractalPattern");
        patternKernel = patternShader.FindKernel("JuliaKernel");
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

        // Last row, output box
        GUILayout.BeginHorizontal();
        // Input knobs
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref radius, 0, 10, radiusKnob);
        FloatKnobOrSlider(ref zoom, .0000000001f, 2, zoomKnob);
        FloatKnobOrSlider(ref bias, 0, 10, biasKnob);
        IntKnobOrSlider(ref maxIterations, 1, 100, maxIterationsKnob);
        IntKnobOrSlider(ref order, 1, 100, orderKnob);
        offsetKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.EndVertical();

        outputTexKnob.SetPosition(DefaultSize.x-20);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        patternShader.SetInts("outputSize", outputSize.x, outputSize.y);
        patternShader.SetInt("maxIterations", maxIterations);
        patternShader.SetInt("order", order);
        patternShader.SetFloat("bias", bias);
        patternShader.SetFloat("zoom", zoom);
        patternShader.SetFloat("radius", radius);
        if (offsetKnob.connected())
        {
            offset = offsetKnob.GetValue<Vector2>();
        } else
        {
            offset = new Vector2(1,1);
        }
        patternShader.SetFloats("offset", offset.x, offset.y);
        patternShader.SetVector("convergeColor", Color.red);
        patternShader.SetVector("divergeColor", Color.black);
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
