using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using UnityEngine;


[Node(false, "Pattern/HSVNode")]
public class HSVNode : Node
{
    public const string ID = "hsvNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "HSV"; } }
    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    [ValueConnectionKnob("Texture", Direction.In, typeof(Texture))]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Texture", Direction.Out, typeof(Texture))]
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

    private void Awake()
    {
        HSVShader = Resources.Load<ComputeShader>("Filters/HSVFilter");
        kernelId = HSVShader.FindKernel("CSMain");
        outputTex = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
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
            return true;
        }
        HSV = new Vector4(hueKnob.GetValue<float>(), satKnob.GetValue<float>(), valKnob.GetValue<float>());
        //Execute HSV compute shader here
        HSVShader.SetVector("HSV", HSV);
        HSVShader.SetTexture(kernelId, "OutputTex", outputTex);
        HSVShader.SetTexture(kernelId, "InputTex", textureInputKnob.GetValue<Texture>());
        HSVShader.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}