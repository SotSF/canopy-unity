
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/AugustNode")]
public class AugustNodeNode : TickingNode
{
    public override string GetID => "AugustNodeNode";
    public override string Title { get { return "AugustNode"; } }

    public override Vector2 DefaultSize { get { return new Vector2(200, 200); } }

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;
    [ValueConnectionKnob("modTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob modTexKnob;

    [ValueConnectionKnob("controlSignal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob controlSignalKnob;
    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    private float controlSignal = 0;
    public RenderTexture outputTex;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/AugustNodeFilter");
        patternKernel = patternShader.FindKernel("PatternKernel");
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
        inputTexKnob.SetPosition(20);
        GUILayout.BeginVertical();
        controlSignalKnob.DisplayLayout();
        if (!controlSignalKnob.connected())
        {
            controlSignal = RTEditorGUI.Slider(controlSignal, 0, 1);
        } else
        {
            controlSignal = controlSignalKnob.GetValue<float>();
        }
        GUILayout.FlexibleSpace();
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
        Texture inputTex = inputTexKnob.GetValue<Texture>();
        if (!inputTexKnob.connected () || inputTex == null)
        {
            outputTexKnob.ResetValue();
            outputSize = Vector2Int.zero;
            if (outputTex != null)
                outputTex.Release();
            return true;
        }
        var inputSize = new Vector2Int(inputTex.width, inputTex.height);
        if (inputSize != outputSize){
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        controlSignal = controlSignalKnob.connected() ? controlSignalKnob.GetValue<float>(): controlSignal;
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetFloat("controlSignal", controlSignal);
        patternShader.SetTexture(patternKernel, "inputTex", inputTex);
        patternShader.SetTexture(patternKernel, "outputTex", outputTex);
        var modTex = modTexKnob.GetValue<Texture>();
        if (modTex == null)
        {
            modTex = inputTex;
        }
        patternShader.SetTexture(patternKernel, "modTex", modTex);
        uint tx,ty,tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
        outputTexKnob.SetValue(outputTex);
        return true;
    }
}
