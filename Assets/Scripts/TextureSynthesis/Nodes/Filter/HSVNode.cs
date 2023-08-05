using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;

using SecretFire.TextureSynth;

using UnityEngine;


[Node(false, "Filter/HSV")]
public class HSVNode : TextureSynthNode
{
    public const string ID = "hsvNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "HSV"; } }
    public override Vector2 DefaultSize { get { return new Vector2(150, 120); } }

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

    public float hue, saturation, value;

    private ComputeShader HSVShader;
    private int kernelId;
    private Vector4 HSV;
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2Int inputSize;
    private void Awake()
    {
        HSVShader = Resources.Load<ComputeShader>("NodeShaders/HSVFilter");
        kernelId = HSVShader.FindKernel("CSMain");
        inputSize = new Vector2Int(0, 0);
        HSV = new Vector4(hue, saturation, value);
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
        GUILayout.BeginVertical();
        textureInputKnob.DisplayLayout();
        FloatKnobOrSlider(ref hue, 0, 1, hueKnob);
        FloatKnobOrSlider(ref saturation, 0, 1, satKnob);
        FloatKnobOrSlider(ref value, 0, 1, valKnob);
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        { // Reset outputs if no texture is available
            if (outputTex != null)
                outputTex.Release();
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            return true;
        }

        inputSize.x = tex.width;
        inputSize.y = tex.height;
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        if (hueKnob.connected())
        {
            hue = hueKnob.GetValue<float>();
        }
        if (satKnob.connected())
        {
            saturation = satKnob.GetValue<float>();
        }
        if (valKnob.connected())
        {
            value = valKnob.GetValue<float>();
        }
        HSV.x = hue;
        HSV.y = saturation;
        HSV.z = value;
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