
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/Superformula")]
public class SuperformulaNode : TickingNode
{
    public override string GetID => "SuperformulaNode";
    public override string Title { get { return "Superformula"; } }

    public override Vector2 DefaultSize { get { return new Vector2(256, 400); } }

    [ValueConnectionKnob("a", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob aKnob;
    [ValueConnectionKnob("b", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob bKnob;
    [ValueConnectionKnob("m1", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob m1Knob;
    [ValueConnectionKnob("m2", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob m2Knob;
    [ValueConnectionKnob("n1", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob n1Knob;
    [ValueConnectionKnob("n2", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob n2Knob;
    [ValueConnectionKnob("n3", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob n3Knob;

    public float a = 30;
    public float b = 30;
    public float m1 = 40;
    public float m2 = 30;
    public float n1 = 15;
    public float n2 = 45;
    public float n3 = 15;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(256, 256);


    private RenderTexture outputTex;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/SuperformulaPattern");
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
        aKnob.DisplayLayout();
        if (!aKnob.connected()) { a = RTEditorGUI.Slider(a, 0.1f, 100); }
        bKnob.DisplayLayout();
        if (!bKnob.connected()) { b = RTEditorGUI.Slider(b, 0.1f, 100); }
        m1Knob.DisplayLayout();
        if (!m1Knob.connected()) { m1 = RTEditorGUI.Slider(m1, 0, 100); }
        m2Knob.DisplayLayout();
        if (!m2Knob.connected()) { m2 = RTEditorGUI.Slider(m2, 0, 100); }
        n1Knob.DisplayLayout();
        if (!n1Knob.connected()) { n1 = RTEditorGUI.Slider(n1, 0, 100); }
        n2Knob.DisplayLayout();
        if (!n2Knob.connected()) { n2 = RTEditorGUI.Slider(n2, 0, 100); }
        n3Knob.DisplayLayout();
        if (!n3Knob.connected()) { n3 = RTEditorGUI.Slider(n3, 0, 100); }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(236);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        a = aKnob.connected() ? aKnob.GetValue<float>(): a;
        b = bKnob.connected() ? bKnob.GetValue<float>() : b;
        m1 = m1Knob.connected() ? m1Knob.GetValue<float>() : m1;
        m2 = m2Knob.connected() ? m2Knob.GetValue<float>() : m2;
        n1 = n1Knob.connected() ? n1Knob.GetValue<float>() : n1;
        n2 = n2Knob.connected() ? n2Knob.GetValue<float>() : n2;
        n3 = n3Knob.connected() ? n3Knob.GetValue<float>() : n3;


        patternShader.SetFloat("a", a);
        patternShader.SetFloat("b", b);
        patternShader.SetFloat("m1", m1);
        patternShader.SetFloat("m2", m2);
        patternShader.SetFloat("n1", n1);
        patternShader.SetFloat("n2", n2);
        patternShader.SetFloat("n3", n3);

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
