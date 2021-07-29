
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

    public override Vector2 DefaultSize { get { return new Vector2(170,180); } }

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


    private ComputeShader graphShader;
    private int gridPointsKernel;
    private int horizontalAxisKernel;
    private int verticalAxisKernel;
    private int graphKernel;
    private RenderTexture graphTexture;

    private Vector2Int outputSize = new Vector2Int(128,128);

    private List<float> timeValues;
    private List<float> signalValues;

    float windowMaxX = 1, windowMinX = -1, windowMaxY = 1, windowMinY = -1;

    private void Awake(){
        timeValues = new List<float>(257);
        signalValues = new List<float>(257);
        graphShader = Resources.Load<ComputeShader>("NodeShaders/GraphView");
        gridPointsKernel = graphShader.FindKernel("gridPoints");
        horizontalAxisKernel = graphShader.FindKernel("horizontalAxis");
        verticalAxisKernel = graphShader.FindKernel("verticalAxis");
        graphKernel = graphShader.FindKernel("graph");
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
        RenderTexture.active = graphTexture;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = null;
    }
    
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        
        // Signal input knob and value label
        GUILayout.BeginHorizontal();
        signalKnob.DisplayLayout();
        GUILayout.Label(string.Format("value: {0:0.0000}", signalKnob.GetValue<float>()));
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();

        //Top/mid/bottom labels
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.Label(string.Format("{0:0.00}", windowMaxY));
        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("{0:0.00}", (windowMaxY+windowMinY)/2));
        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("{0:0.00}", windowMinY));
        GUILayout.EndVertical();

        GUILayout.Box(graphTexture, GUILayout.MaxWidth(256), GUILayout.MaxHeight(256));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        outputTexKnob.SetPosition(DefaultSize.x-20);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
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

        //if (windowMaxX <= windowMinX || windowMaxY <= windowMinY)
        //{
        //    return true;
        //}
        //pollKnobs();

        // Set graph params
        graphShader.SetInt("minTickSpacing", 5);
        graphShader.SetInts("texSize", outputSize.x, outputSize.y);
        graphShader.SetFloats("xValues", timeValues.ToArray());
        graphShader.SetFloats("yValues", signalValues.ToArray());
        graphShader.SetInt("numPoints", timeValues.Count);

        if (timeValues.Count > 0 && signalValues.Count > 0)
        {
            var minX = timeValues.Min();
            var maxX = timeValues.Max();
            var minY = signalValues.Min();
            var maxY = signalValues.Max();
            windowMinX = minX - (maxX - minX)/20;
            windowMaxX = maxX + (maxX - minX) / 20;
            windowMinY = minY - (maxY - minY) / 20;
            windowMaxY = maxY + (maxY - minY) / 20;
            graphShader.SetFloats("windowMin", windowMinX, windowMinY);
            graphShader.SetFloats("windowMax", windowMaxX, windowMaxY);
        }

        // Set colors
        graphShader.SetVector("lineColor", Color.cyan);
        graphShader.SetVector("backgroundColor", new Color(0.1f, 0.1f, 0.1f, 1));
        graphShader.SetVector("labelColor", Color.white);

        // Set render texture
        graphShader.SetTexture(gridPointsKernel, "outputTex", graphTexture);
        graphShader.SetTexture(horizontalAxisKernel, "outputTex", graphTexture);
        graphShader.SetTexture(verticalAxisKernel, "outputTex", graphTexture);
        graphShader.SetTexture(graphKernel, "outputTex", graphTexture);

        // Dispatch kernels
        uint tx, ty, tz;
        graphShader.GetKernelThreadGroupSizes(gridPointsKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        //this.TimedDebug("Drawing grid points");
        graphShader.Dispatch(gridPointsKernel, threadGroupX, threadGroupY, 1);

        //this.TimedDebugFmt("minX: {0}, maxX: {1}, minY: {2}, maxY: {3}", 2, windowMinX, windowMaxX, windowMinY, windowMaxY);
        if (windowMinY < 0 && windowMaxY > 0)
        {
            //this.TimedDebug("Drawing horizontal axis");
            graphShader.Dispatch(horizontalAxisKernel, Mathf.CeilToInt(outputSize.x / 256f), 1, 1);
        }
        if (windowMinX < 0 && windowMaxX > 0)
        {
            //this.TimedDebug("Drawing vertical axis");
            graphShader.Dispatch(verticalAxisKernel, 1, Mathf.CeilToInt(outputSize.y / 256f), 1);
        }

        if (signalValues.Count > 0)
        {
            //this.TimedDebug("Drawing graph points");
            graphShader.Dispatch(graphKernel, 1, 1, 1);
        }

        outputTexKnob.SetValue(graphTexture);
        return true;
    }
}
