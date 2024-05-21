
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Pattern/ConcentricCircles")]
public class ConcentricCirclesNode : TickingNode
{
    public override string GetID => "ConcentricCirclesNode";
    public override string Title { get { return "ConcentricCircles"; } }
    private Vector2 _DefaultSize = new Vector2(250, 200);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("ManualTrigger", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob ManualTriggerKnob;
    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int clearKernel;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(256,256);
    public RenderTexture outputTex;

    private List<Circle> circles = new List<Circle>();
    private bool Fill = false;
    private bool Continuous = false;
    private bool Invert = false;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/ConcentricCirclesPattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
        clearKernel = patternShader.FindKernel("ClearKernel");
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
        if (EventKnobOrButton("Manual trigger", ManualTriggerKnob))
        {
            circles.Add(new Circle());
        }
        Fill = RTEditorGUI.Toggle(Fill, new GUIContent("Fill", "Fill circles"));
        Continuous = RTEditorGUI.Toggle(Continuous, new GUIContent("Continuous", "Continuous Add"));
        Invert = RTEditorGUI.Toggle(Invert, new GUIContent("Invert", "Invert directionality"));

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(230);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        if (Continuous)
        {
            circles.Add(new Circle());
        }

        patternShader.SetBool("InvertDirection", Invert);
        patternShader.SetBool("Fill", Fill);
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetTexture(clearKernel, "outputTex", outputTex);
        patternShader.SetTexture(patternKernel, "outputTex", outputTex);
        uint tx,ty,tz;

        patternShader.GetKernelThreadGroupSizes(clearKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(clearKernel, threadGroupX, threadGroupY, 1);

        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);

        foreach (Circle c in circles)
        {
            patternShader.SetFloat("radius", c.radius);
            patternShader.SetFloat("fade", c.fade);
            patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
            c.Update();
        }

        circles.RemoveAll(c => c.radius > outputSize.x);

        outputTexKnob.SetValue(outputTex);
        return true;
    }

    private void AddCircle()
    {

    }
    private class Circle
    {
        public float radius = 1;
        public float fade = 0;

        public void Update()
        {
            this.radius++;
            this.fade += 0.005f;
        }
    }
}
