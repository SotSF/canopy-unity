
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Pattern/Drops")]
public class DropsNode : TickingNode
{
    public override string GetID => "DropsNode";
    public override string Title { get { return "Drops"; } }
    private Vector2 _DefaultSize = new Vector2(256, 256); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Interval", Direction.In, typeof(int), NodeSide.Left)]
    public ValueConnectionKnob IntervalKnob;
    [ValueConnectionKnob("Fade", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob FadeKnob;
    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(256, 256);
    private int Interval = 5;
    private float Fade = 5;
    private bool Fill = false;
    public RenderTexture outputTex;

    private List<Drop> drops = new List<Drop>();
    private int tick = 0;

    public override void DoInit(){
        

        patternShader = Resources.Load<ComputeShader>("NodeShaders/DropsPattern");
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
        IntervalKnob.DisplayLayout();
        if (!IntervalKnob.connected())
        {
            Interval = RTEditorGUI.IntSlider(Interval, 1, 10);
        } else
        {
            Interval = IntervalKnob.GetValue<int>();
        }
        FadeKnob.DisplayLayout();
        if (!FadeKnob.connected())
        {
            Fade = RTEditorGUI.Slider(Fade, 1, 10);
        } else
        {
            Fade = FadeKnob.GetValue<float>();
        }
        Fill = RTEditorGUI.Toggle(Fill, new GUIContent("Fill", "Fill circles"));

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

    public override bool DoCalc()
    {
       

        Interval = IntervalKnob.connected() ? IntervalKnob.GetValue<int>(): Interval;
        Fade = FadeKnob.connected() ? FadeKnob.GetValue<float>(): Fade;

        if (tick % Interval == 0)
        {
            this.drops.Add(new Drop(outputSize));
        }

        float fadeVal = Fade / 200;

        patternShader.SetBool("fill", Fill);
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetTexture(patternKernel, "outputTex", outputTex);
        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        foreach (Drop d in this.drops)
        {
            patternShader.SetInts("center", d.center.x, d.center.y);
            patternShader.SetFloat("radius", d.radius);
            patternShader.SetFloats("hsv", d.hsvColor.x, d.hsvColor.y, d.hsvColor.z);
            patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);

            d.Update(fadeVal);
        }

        this.drops.RemoveAll(d => d.hsvColor.z <= 0);
        this.tick++;
        outputTexKnob.SetValue(outputTex);
        return true;
    }

    private class Drop
    {
        public Vector2Int center;
        public float radius;
        public Vector3 hsvColor;

        public Drop(Vector2Int canvasSize)
        {
            this.center = new Vector2Int(Random.Range(0, canvasSize.x), Random.Range(0, canvasSize.y));
            this.radius = 1;
            this.hsvColor = new Vector3(0.6f, 1, 1);
        }

        public void Update(float fade)
        {
            this.radius += 0.5f;
            this.hsvColor.z -= fade;
            if (this.hsvColor.z < 0) { this.hsvColor.z = 0; }
        }

    }
}
