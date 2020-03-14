
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/PolarRose")]
public class PolarRoseNode : TickingNode
{
    public override string GetID => "PolarRoseNode";
    public override string Title { get { return "PolarRose"; } }

    public override Vector2 DefaultSize { get { return new Vector2(100, 100); } }

    [ValueConnectionKnob("r", Direction.In, typeof(float))]
    public ValueConnectionKnob rInputKnob;
    public float r = 3f;

    [ValueConnectionKnob("k", Direction.In, typeof(float))]
    public ValueConnectionKnob kInputKnob;
    public float k = 3f;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    public RenderTexture outputTex;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/PolarRosePattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
    }
    private void InitializeRenderTexture()
    {
        outputSize.x = (int)DefaultSize.x;
        outputSize.y = (int)DefaultSize.y;
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
        FloatKnobOrSlider(ref r, 0, 10, rInputKnob);
        FloatKnobOrSlider(ref k, 0, 30, kInputKnob);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(180);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        InitializeRenderTexture();

        r = rInputKnob.connected() ? rInputKnob.GetValue<float>() : r;
        k = kInputKnob.connected() ? kInputKnob.GetValue<float>() : k;

        patternShader.SetFloat("rmod", r);
        patternShader.SetFloat("k", k);
        patternShader.SetFloats("dims", outputSize.x, outputSize.y);
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
