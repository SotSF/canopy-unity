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
    private int kernelId;
    public RenderTexture outputTex;

    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2 offset = Vector2.zero;

    public bool smoothTransitions;
    public float speed, angle;

    private void Awake()
    {
        panShader = Resources.Load<ComputeShader>("FilterShaders/PanFilter");
        kernelId = panShader.FindKernel("CSMain");
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
        textureInputKnob.DisplayLayout();
        
        speedInputKnob.DisplayLayout(new GUIContent("Speed", "The speed to pan in image widths/second"));
        if (!speedInputKnob.connected())
        {
            speed = RTEditorGUI.Slider(speed, -1, 1);
        }
        angleInputKnob.DisplayLayout(new GUIContent("Angle", "The angle to pan in radians"));
        if (!angleInputKnob.connected())
        {
            angle = RTEditorGUI.Slider(angle, 0, 6.2831f);
        }
        smoothTransitions = RTEditorGUI.Toggle(smoothTransitions, new GUIContent("Smooth", "Whether the image panning should use bilinear filtering to produce smooth transitions"));
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Offset: ({0:0.00}, {1:0.00})", offset.x, offset.y));
        if (GUILayout.Button("Reset"))
        {
            offset = Vector2.zero;
        }
        GUILayout.EndHorizontal();
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();
        //GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

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

        var inputSize = new Vector2Int(tex.width, tex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        speed = speedInputKnob.connected() ? speedInputKnob.GetValue<float>() : speed;
        angle = angleInputKnob.connected() ? angleInputKnob.GetValue<float>() : angle;

        var r = speed * tex.width * Time.deltaTime;
        offset += new Vector2(r * Mathf.Cos(angle), r * Mathf.Sin(angle));

        if (offset.x > tex.width/2)
        {
            offset.x = -tex.width / 2;
        } else if (offset.x < -tex.width / 2)
        {
            offset.x = tex.width / 2;
        }
        if (offset.y > tex.height / 2)
        {
            offset.y = -tex.height / 2;
        } else if (offset.y < -tex.height / 2)
        {
            offset.y = tex.height / 2;
        }

        //Execute compute shader
        panShader.SetInt("width", tex.width);
        panShader.SetInt("height", tex.height);
        panShader.SetBool("smoothTransitions", smoothTransitions);
        panShader.SetFloat("theta", angle);
        panShader.SetFloat("time", Time.time);
        panShader.SetFloat("speed", speed);
        panShader.SetFloats("offset", offset.x, offset.y);
        panShader.SetFloat("frameTime", Time.deltaTime);
        panShader.SetTexture(kernelId, "OutputTex", outputTex);
        panShader.SetTexture(kernelId, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        panShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
