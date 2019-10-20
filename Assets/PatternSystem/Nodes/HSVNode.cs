using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using UnityEngine;


[Node(false, "Filter/HSV")]
public class HSVNode : Node
{
    public const string ID = "hsvNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "HSV"; } }
    public override Vector2 DefaultSize { get { return new Vector2(100, 100); } }

    [ValueConnectionKnob("Texture", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Texture", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("H", Direction.In, "Float")]
    public ValueConnectionKnob hueKnob;
    [ValueConnectionKnob("S", Direction.In, "Float")]
    public ValueConnectionKnob satKnob;
    [ValueConnectionKnob("V", Direction.In, "Float")]
    public ValueConnectionKnob valKnob;

    private ComputeShader HSVShader;
    private int kernelId;
    private Vector4 HSV;
    public RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    private void Awake()
    {
        HSVShader = Resources.Load<ComputeShader>("FilterShaders/HSVFilter");
        kernelId = HSVShader.FindKernel("CSMain");
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
        hueKnob.DisplayLayout();
        satKnob.DisplayLayout();
        valKnob.DisplayLayout();
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

        HSV = new Vector4(hueKnob.GetValue<float>(), 
                          satKnob.GetValue<float>(), 
                          valKnob.GetValue<float>());

        //Execute HSV compute shader here
        HSVShader.SetVector("HSV", HSV);
        HSVShader.SetTexture(kernelId, "OutputTex", outputTex);
        HSVShader.SetTexture(kernelId, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        HSVShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}