
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Filter/ChromaKey")]
public class ChromaKeyNode : TextureSynthNode
{
    public override string GetID => "ChromaKeyNode";
    public override string Title { get { return "ChromaKey"; } }
    private Vector2 _DefaultSize =new Vector2(200, 150);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;
    [ValueConnectionKnob("keyColor", Direction.In, typeof(Vector4), NodeSide.Left)]
    public ValueConnectionKnob keyColorKnob;
    [ValueConnectionKnob("sensitivity", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob sensitivityKnob;
    public float sensitivity;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    public RenderTexture outputTex;

    public override void DoInit(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/ChromaKeyFilter");
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

    float h, s, v;
    public override void NodeGUI()
    {
        inputTexKnob.SetPosition(20);

        GUILayout.BeginVertical();

        // Top row: control sliders
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref sensitivity, 0, 100, sensitivityKnob, GUILayout.MaxWidth(DefaultSize.x-60));
        keyColorKnob.DisplayLayout();
        if (!keyColorKnob.connected())
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("H");
            h = RTEditorGUI.Slider(h, 0, 1, GUILayout.MaxWidth(DefaultSize.x - 60));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("S");
            s = RTEditorGUI.Slider(s, 0, 1, GUILayout.MaxWidth(DefaultSize.x - 60));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("V");
            v = RTEditorGUI.Slider(v, 0, 1, GUILayout.MaxWidth(DefaultSize.x - 60));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        // Bottom row: output image
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
        sensitivity = sensitivityKnob.connected() ? sensitivityKnob.GetValue<float>() : sensitivity;
        var color = Color.HSVToRGB(h, s, v);
        patternShader.SetFloat("sensitivity", sensitivity);
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetVector("keyColor", color);
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
