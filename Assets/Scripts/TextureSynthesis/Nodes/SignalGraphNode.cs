
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Node(false, "Signal/SignalGraph")]
public class SignalGraphNode : TickingNode
{
    public override string GetID => "SignalGraphNode";
    public override string Title { get { return "SignalGraph"; } }

    public override Vector2 DefaultSize { get { return new Vector2(280,400); } }

    [ValueConnectionKnob("signal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob signalKnob;

    //[ValueConnectionKnob("minX", Direction.In, typeof(float), NodeSide.Left)]
    //public ValueConnectionKnob windowMinXKnob;
    //public float windowMinX = -1;

    //[ValueConnectionKnob("maxX", Direction.In, typeof(float), NodeSide.Left)]
    //public ValueConnectionKnob windowMaxXKnob;
    //public float windowMaxX = 1;

    //[ValueConnectionKnob("minY", Direction.In, typeof(float), NodeSide.Left)]
    //public ValueConnectionKnob windowMinYKnob;
    //public float windowMinY = -1;

    //[ValueConnectionKnob("maxY", Direction.In, typeof(float), NodeSide.Left)]
    //public ValueConnectionKnob windowMaxYKnob;
    //public float windowMaxY = 1;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;


    private ComputeShader patternShader;
    private int gridPointsKernel;
    private int horizontalAxisKernel;
    private int verticalAxisKernel;
    private int graphKernel;
    private RenderTexture graphTexture;

    private Vector2Int outputSize = new Vector2Int(256,256);

    private List<float> timeValues;
    private List<float> signalValues;

    private void Awake(){
        timeValues = new List<float>(257);
        signalValues = new List<float>(257);
        patternShader = Resources.Load<ComputeShader>("NodeShaders/GraphView");
        gridPointsKernel = patternShader.FindKernel("gridPoints");
        horizontalAxisKernel = patternShader.FindKernel("horizontalAxis");
        verticalAxisKernel = patternShader.FindKernel("verticalAxis");
        graphKernel = patternShader.FindKernel("graph");
        InitializeRenderTexture();
    }

    private void InitializeRenderTexture()
    {
        if (graphTexture != null)
        {
            Debug.Log("Releasing non-null rendertexture");
            graphTexture.Release();
        }
        graphTexture = new RenderTexture(outputSize.x, outputSize.y, 0);
        graphTexture.enableRandomWrite = true;
        graphTexture.Create();
        Debug.Log("Clearing rendertexture");
        RenderTexture.active = graphTexture;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = null;
    }
    
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        signalKnob.DisplayLayout();
        //FloatKnobOrSlider(ref windowMinX, -100, 100, windowMinXKnob);
        //FloatKnobOrSlider(ref windowMinY, -100, 100, windowMinYKnob);
        //FloatKnobOrSlider(ref windowMaxX, -100, 100, windowMaxXKnob);
        //FloatKnobOrSlider(ref windowMaxY, -100, 100, windowMaxYKnob);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        this.TimedDebug("Drawing UI box");
        GUILayout.Box(graphTexture, GUILayout.MaxWidth(256), GUILayout.MaxHeight(256));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(180);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public void pollKnobs()
    {
        //if (windowMinXKnob.connected())
        //{
        //    windowMinX = windowMinXKnob.GetValue<float>();
        //}
        //if (windowMinYKnob.connected())
        //{
        //    windowMinY = windowMinYKnob.GetValue<float>();
        //}
        //if (windowMaxXKnob.connected())
        //{
        //    windowMaxX = windowMaxXKnob.GetValue<float>();
        //}
        //if (windowMaxYKnob.connected())
        //{
        //    windowMaxY = windowMaxYKnob.GetValue<float>();
        //}
    }

    float lastCalc = 0;
    public override bool Calculate()
    {
        // Only calculate once per frame
        if (Time.time - lastCalc < Time.deltaTime)
        {
            outputTexKnob.SetValue(graphTexture);
            return true;
        }
        lastCalc = Time.time;

        // Store signal values
        if (signalKnob.connected())
        {
            float signal = signalKnob.GetValue<float>();
            if (!(float.IsNaN(signal) || float.IsInfinity(signal)))
            {
                signalValues.Add(signalKnob.GetValue<float>());
                timeValues.Add(Time.time);
            }
        }
        if (signalValues.Count > 256)
        {
            signalValues.RemoveAt(0);
            timeValues.RemoveAt(0);
        }

        float windowMaxX = 1, windowMinX = -1, windowMaxY = 1, windowMinY = -1;
        //if (windowMaxX <= windowMinX || windowMaxY <= windowMinY)
        //{
        //    return true;
        //}
        //pollKnobs();

        // Set graph params
        patternShader.SetInt("minTickSpacing", 5);
        patternShader.SetInts("texSize", outputSize.x, outputSize.y);
        patternShader.SetFloats("xValues", timeValues.ToArray());
        patternShader.SetFloats("yValues", signalValues.ToArray());
        patternShader.SetInt("numPoints", timeValues.Count);

        if (timeValues.Count > 0 && signalValues.Count > 0)
        {
            windowMinX = timeValues.Min() - 1;
            windowMaxX = timeValues.Max() + 1;
            windowMinY = signalValues.Min() - 1;
            windowMaxY = signalValues.Max() + 1;
            patternShader.SetFloats("windowMin", windowMinX, windowMinY);
            patternShader.SetFloats("windowMax", windowMaxX, windowMaxY);
        }

        // Set colors
        patternShader.SetVector("lineColor", Color.cyan);
        patternShader.SetVector("backgroundColor", new Color(0.1f, 0.1f, 0.1f, 1));
        patternShader.SetVector("labelColor", Color.white);

        // Set render texture
        patternShader.SetTexture(gridPointsKernel, "outputTex", graphTexture);
        patternShader.SetTexture(horizontalAxisKernel, "outputTex", graphTexture);
        patternShader.SetTexture(verticalAxisKernel, "outputTex", graphTexture);
        patternShader.SetTexture(graphKernel, "outputTex", graphTexture);

        // Dispatch kernels
        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(gridPointsKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        //this.TimedDebug("Drawing grid points");
        patternShader.Dispatch(gridPointsKernel, threadGroupX, threadGroupY, 1);

        //this.TimedDebugFmt("minX: {0}, maxX: {1}, minY: {2}, maxY: {3}", 2, windowMinX, windowMaxX, windowMinY, windowMaxY);
        if (windowMinY < 0 && windowMaxY > 0)
        {
            //this.TimedDebug("Drawing horizontal axis");
            patternShader.Dispatch(horizontalAxisKernel, Mathf.CeilToInt(outputSize.x / 256f), 1, 1);
        }
        if (windowMinX < 0 && windowMaxX > 0)
        {
            //this.TimedDebug("Drawing vertical axis");
            patternShader.Dispatch(verticalAxisKernel, 1, Mathf.CeilToInt(outputSize.y / 256f), 1);
        }

        if (signalValues.Count > 0)
        {
            //this.TimedDebug("Drawing graph points");
            patternShader.Dispatch(graphKernel, 1, 1, 1);
        }

        outputTexKnob.SetValue(graphTexture);
        return true;
    }
}
