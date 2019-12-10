using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;


[Node(false, "Filter/Pan")]
public class PanNode : TickingNode { 
    public const string ID = "panNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Pan"; } }
    public override Vector2 DefaultSize { get { return new Vector2(200, 175); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Speed", Direction.In, typeof(float))]
    public ValueConnectionKnob speedInputKnob;

    [ValueConnectionKnob("Angle", Direction.In, typeof(float))]
    public ValueConnectionKnob angleInputKnob;


    private ComputeShader panShader;
    private int bilinearMirrorKernel;
    private int bilinearRepeatKernel;
    private int pointMirrorKernel;
    private int pointRepeatKernel;
    public RenderTexture outputTex;

    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2 offset = Vector2.zero;

    public bool smoothTransitions;
    public bool mirror;
    public float speed, angle;

    private void Awake()
    {
        panShader = Resources.Load<ComputeShader>("NodeShaders/PanFilter");
        bilinearMirrorKernel = panShader.FindKernel("BilinearMirror");
        bilinearRepeatKernel = panShader.FindKernel("BilinearRepeat");
        pointMirrorKernel = panShader.FindKernel("PointMirror");
        pointRepeatKernel = panShader.FindKernel("PointRepeat");
    }

    private void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        //GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        
        speedInputKnob.DisplayLayout(new GUIContent("Speed", "The speed to pan in image widths/second"));
        if (!speedInputKnob.connected())
        {
            speed = RTEditorGUI.Slider(speed, -32, 32);
        }
        angleInputKnob.DisplayLayout(new GUIContent("Angle", "The angle to pan in radians"));
        if (!angleInputKnob.connected())
        {
            angle = RTEditorGUI.Slider(angle, 0, 6.2831f);
        }
        GUILayout.BeginHorizontal();
        smoothTransitions = RTEditorGUI.Toggle(smoothTransitions, new GUIContent("Smooth", "Whether the image panning should use bilinear filtering to produce smooth transitions"));
        mirror = RTEditorGUI.Toggle(mirror, new GUIContent("Mirror", "Use mirror wraping at texture edges"));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Offset: ({0:0.00}, {1:0.00})", offset.x, offset.y));
        if (GUILayout.Button("Reset"))
        {
            offset = Vector2.zero;
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        //GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    float lastStep = 0;
    public override bool Calculate()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        { // Reset outputs if no texture is available
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            if (outputTex != null)
                outputTex.Release();
            return true;
        }
        // Guard against multiple Calculate()'s per frame
        if (Time.time - lastStep > Time.deltaTime)
        {
            lastStep = Time.time;
        }
        else
        {
            textureOutputKnob.SetValue(outputTex);
            return true;
        }

        var inputSize = new Vector2Int(tex.width, tex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        speed = speedInputKnob.connected() ? speedInputKnob.GetValue<float>() : speed;
        angle = angleInputKnob.connected() ? angleInputKnob.GetValue<float>() : angle;


        // Keep offset bounded by (2x) dimensions so that floating point coverage doesn't decrease
        // over long pans. use 2x so that mirrored textures don't jump on resetting the offset
        var r = speed * Time.deltaTime;
        offset += new Vector2(r * Mathf.Cos(angle), r * Mathf.Sin(angle));
        Vector2 mirrorSafeBounds = 2*new Vector2(tex.width-1, tex.height-1);
        if (offset.x > mirrorSafeBounds.x)
        {
            offset.x -= mirrorSafeBounds.x;
        }
        else if (offset.x < -mirrorSafeBounds.x)
        {
            offset.x += mirrorSafeBounds.x;
        }
        if (offset.y > mirrorSafeBounds.y)
        {
            offset.y -= mirrorSafeBounds.y;
        }
        else if (offset.y < -mirrorSafeBounds.y)
        {
            offset.y += mirrorSafeBounds.y;
        }

        int panKernel = 0;
        if (smoothTransitions && mirror)
        {
            panKernel = bilinearMirrorKernel;
        } else if (smoothTransitions && !mirror)
        {
            panKernel = bilinearRepeatKernel;
        } else if (!smoothTransitions && mirror)
        {
            panKernel = pointMirrorKernel;
        } else if (!smoothTransitions && !mirror)
        {
            panKernel = pointRepeatKernel;
        }

        panShader.SetInt("width", tex.width);
        panShader.SetInt("height", tex.height);
        panShader.SetFloats("offset", offset.x, offset.y);
        panShader.SetTexture(panKernel, "OutputTex", outputTex);
        panShader.SetTexture(panKernel, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        panShader.Dispatch(panKernel, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
