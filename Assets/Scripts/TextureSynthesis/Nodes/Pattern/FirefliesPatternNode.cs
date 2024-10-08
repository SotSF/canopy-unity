
using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using NodeEditorFramework.Utilities;

[Node(false, "Pattern/FirefliesPattern")]
public class jptestNode : TickingNode
{
    public override string GetID => "FirefliesPattern";
    public override string Title { get { return "Fireflies"; } }
    private Vector2 _DefaultSize = new Vector2(200, 200);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("jitter", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob ManualJitterKnob;
    [ValueConnectionKnob("count", Direction.In, typeof(int), NodeSide.Left)]
    public ValueConnectionKnob countKnob;
    [ValueConnectionKnob("trail", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob trailKnob;
    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int fadeKernel;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(256,256);
    public RenderTexture outputTex;

    private int _count;
    private int Count {
        get {
            return _count;
        }
        set {
            _count = value;
            if (_count > objects.Count)
            {
                while (objects.Count < Count)
                {
                    objects.Add(new PatternObject(outputSize));
                }
            } else
            {
                objects.RemoveRange(_count, objects.Count - _count);
            }
        }
    }
    private float Trail = 0;
    private List<PatternObject> objects = new List<PatternObject>();
    private int tick = 0;

    public override void DoInit(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/FirefliesPattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
        fadeKernel = patternShader.FindKernel("FadeKernel");
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
        if (EventKnobOrButton("Manual jitter", ManualJitterKnob))
        {
            JitterFlies();
        }

        countKnob.DisplayLayout();
        if (!countKnob.connected())
        {
            Count = RTEditorGUI.IntSlider(Count, 1, 100);
        }
        else
        {
            Count = countKnob.GetValue<int>();
        }
        trailKnob.DisplayLayout();
        if (!trailKnob.connected())
        {
            Trail = RTEditorGUI.Slider(Trail, 0, 0.1f);
        }
        else
        {
            Trail = trailKnob.GetValue<float>();
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

    public override bool DoCalc()
    {
        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetTexture(fadeKernel, "outputTex", outputTex);
        patternShader.SetTexture(patternKernel, "outputTex", outputTex);

        uint tx,ty,tz;

        patternShader.SetFloat("trail", Trail);
        patternShader.GetKernelThreadGroupSizes(fadeKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(fadeKernel, threadGroupX, threadGroupY, 1);

        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);

        foreach (PatternObject obj in this.objects)
        {
            patternShader.SetInts("xy", obj.pos.x, obj.pos.y);
            patternShader.SetInt("size", obj.size);
            patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
            if (tick % obj.tickMod == 0) { obj.updatePos(); }
        }

        
        outputTexKnob.SetValue(outputTex);
        tick++;

        return true;
    }

    private void JitterFlies()
    {
        foreach (PatternObject o in objects)
        {
            o.pos.x += Random.Range(-1, 1) < 0 ? -50 : 50;
            o.pos.y += Random.Range(-1, 1) < 0 ? -50 : 50;

            o.pos.x = o.pos.x < 0 ? 0 : (o.pos.x >= outputSize.x ? outputSize.x - 1 : o.pos.x);
            o.pos.y = o.pos.y < 0 ? 0 : (o.pos.x >= outputSize.y ? outputSize.y - 1 : o.pos.y);
        }
    }

    private class PatternObject {
        public Vector2Int pos;
        public int size;
        public Vector2Int outputSize;
        public int tickMod = 1;

        public PatternObject(Vector2Int outputSize) {
            this.outputSize = outputSize;
            this.pos = new Vector2Int(
                Random.Range(0,outputSize.x), 
                Random.Range(0,outputSize.y)
            );
            this.size = Random.Range(1, 3);
            this.tickMod = Random.Range(1, 4);
        }

        public void updatePos() {
            pos.x += Random.Range(-1,1) < 0 ? -1 : 1;
            pos.y += Random.Range(-1,1) < 0 ? -1 : 1; 

            pos.x = pos.x < 0 ? 0 : (pos.x >= outputSize.x ? outputSize.x - 1 : pos.x);
            pos.y = pos.y < 0 ? 0 : (pos.x >= outputSize.y ? outputSize.y - 1 : pos.y);
        }
    }

   
}
