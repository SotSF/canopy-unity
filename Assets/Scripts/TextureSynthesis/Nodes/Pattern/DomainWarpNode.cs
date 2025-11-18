
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/DomainWarp")]
public class DomainWarpNode : TickingNode
{
    public override string GetID => "DomainWarpNode";
    public override string Title { get { return "DomainWarp"; } }
    private Vector2 _DefaultSize = new Vector2(300, 300); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("timeMultiplier", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob timeMultiplierKnob;
    public float timeScale;

    [ValueConnectionKnob("H", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob hKnob;
    public float h = 1;

    [ValueConnectionKnob("octaves", Direction.In, typeof(int), NodeSide.Left)]
    public ValueConnectionKnob octavesKnob;
    public int octaves = 4;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(256, 256);
    private RenderTexture outputTex;

    public override void DoInit(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/DomainWarpPattern");
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

        FloatKnobOrSlider(ref h, 0, 1, hKnob);
        FloatKnobOrSlider(ref timeScale, 0, 255, timeMultiplierKnob);
        IntKnobOrSlider(ref octaves, 1, 12, octavesKnob);
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(256), GUILayout.MaxHeight(256));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(180);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (hKnob.connected())
        {
            h = hKnob.GetValue<float>();
        }
        if (timeMultiplierKnob.connected())
        {
            timeScale = timeMultiplierKnob.GetValue<float>();
        }
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetFloat("h", h);
        patternShader.SetFloat("time", Time.time);
        patternShader.SetFloat("timeScale", timeScale);
        patternShader.SetInt("octaves", octaves);
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
