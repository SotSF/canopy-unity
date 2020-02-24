
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Filter/GradientFilter")]
public class GradientFilterNode : Node
{
    public override string GetID => "GradientFilterNode";
    public override string Title { get { return "Gradient"; } }

    public override Vector2 DefaultSize { get { return new Vector2(256, 256); } }

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;
    [ValueConnectionKnob("startHue", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob startHueKnob;
    [ValueConnectionKnob("endHue", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob endHueKnob;
    [ValueConnectionKnob("offset", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob offsetKnob;
    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    private float offset = 0;
    private float startHue = 0;
    private float endHue = 1;
    public RenderTexture outputTex;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/GradientFilter");
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
        startHueKnob.DisplayLayout();
        if (!startHueKnob.connected())
        {
            startHue = RTEditorGUI.Slider(startHue, 0, 1);
        } else
        {
            startHue = startHueKnob.GetValue<float>();
        }
        endHueKnob.DisplayLayout();
        if (!endHueKnob.connected())
        {
            endHue = RTEditorGUI.Slider(endHue, 0, 1);
        } else
        {
            endHue = endHueKnob.GetValue<float>();
        }
        offsetKnob.DisplayLayout();
        if (!offsetKnob.connected())
        {
            offset = RTEditorGUI.Slider(offset, 0, 1);
        } else
        {
            offset = offsetKnob.GetValue<float>();
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
        startHue = startHueKnob.connected() ? startHueKnob.GetValue<float>(): startHue;
        endHue = endHueKnob.connected() ? endHueKnob.GetValue<float>(): endHue;
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetFloat("startHue", startHue);
        patternShader.SetFloat("endHue", endHue);
        patternShader.SetFloat("offset", offset * Mathf.Sqrt(Mathf.Pow(outputSize.x, 2) + Mathf.Pow(outputSize.y, 2)));
        patternShader.SetTexture(patternKernel, "inputTex", inputTex);
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
