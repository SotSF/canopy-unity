using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;


[Node(false, "Filter/Pan")]
public class PanNode : Node
{
    public const string ID = "panNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Pan"; } }
    public override Vector2 DefaultSize { get { return new Vector2(200, 150); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture))]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Speed", Direction.In, typeof(float))]
    public ValueConnectionKnob speedInputKnob;

    [ValueConnectionKnob("Angle", Direction.In, typeof(float))]
    public ValueConnectionKnob angleInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader PanShader;
    private int kernelId;
    public RenderTexture outputTex;

    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2 offset = Vector2.zero;

    public bool smoothTransitions;
    public float speed, angle;

    private void Awake()
    {
        PanShader = Resources.Load<ComputeShader>("FilterShaders/PanFilter");
        kernelId = PanShader.FindKernel("CSMain");
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
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
        GUILayout.EndVertical();
        textureOutputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

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
        PanShader.SetInt("width", tex.width);
        PanShader.SetInt("height", tex.height);
        PanShader.SetBool("smoothTransitions", smoothTransitions);
        PanShader.SetFloat("theta", angle);
        PanShader.SetFloat("time", Time.time);
        PanShader.SetFloat("speed", speed);
        PanShader.SetFloats("offset", offset.x, offset.y);
        PanShader.SetFloat("frameTime", Time.deltaTime);
        PanShader.SetTexture(kernelId, "OutputTex", outputTex);
        PanShader.SetTexture(kernelId, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        PanShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}