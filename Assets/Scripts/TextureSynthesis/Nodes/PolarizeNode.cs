using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using UnityEngine;


[Node(false, "Filter/Polarize")]
public class PolarizeNode : Node
{
    public const string ID = "polarizeNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Polarize"; } }
    public override Vector2 DefaultSize { get { return new Vector2(75, 75); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture))]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader PolarizeShader;
    private int kernelId;
    private Vector4 HSV;
    public RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    private void Awake()
    {
        PolarizeShader = Resources.Load<ComputeShader>("NodeShaders/PolarizeFilter");
        kernelId = PolarizeShader.FindKernel("CSMain");
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
            return true;
        }

        var inputSize = new Vector2Int(tex.width, tex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }

        //Execute compute shader
        PolarizeShader.SetInt("width", tex.width);
        PolarizeShader.SetInt("height", tex.height);
        PolarizeShader.SetTexture(kernelId, "OutputTex", outputTex);
        PolarizeShader.SetTexture(kernelId, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        PolarizeShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}