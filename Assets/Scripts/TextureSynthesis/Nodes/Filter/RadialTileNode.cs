using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Polar counterpart to cartesian tiling: samples the input texture around the circle,
/// with angular/radial repeat counts, rotation, an optional inner hole, and standard
/// Tile/Mirror/Clamp wrap semantics.
/// </summary>
[Node(false, "Filter/RadialTile")]
public class RadialTileNode : TextureSynthNode
{
    public const string ID = "radialTileNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "RadialTile"; } }

    private Vector2 _DefaultSize = new Vector2(200, 200);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("InTex", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("OutTex", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Repeats", Direction.In, "Float")]
    public ValueConnectionKnob repeatsKnob;
    [ValueConnectionKnob("RScale", Direction.In, "Float")]
    public ValueConnectionKnob radialScaleKnob;
    [ValueConnectionKnob("Rotation", Direction.In, "Float")]
    public ValueConnectionKnob rotationKnob;
    [ValueConnectionKnob("Inner", Direction.In, "Float")]
    public ValueConnectionKnob innerRadiusKnob;

    public float angularRepeats = 6f;
    public float radialScale = 1f;
    public float rotation = 0f;
    public float innerRadius = 0f;
    public RadioButtonSet wrapMode = new RadioButtonSet(0, "Tile", "Mirror", "Clamp");

    private ComputeShader tileShader;
    private int kernelId;
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    public override void DoInit()
    {
        tileShader = Resources.Load<ComputeShader>("NodeShaders/RadialTileFilter");
        kernelId = tileShader.FindKernel("RadialTile");
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
        FloatKnobOrSlider(ref angularRepeats, 1f, 32f, repeatsKnob);
        FloatKnobOrSlider(ref radialScale, 0.1f, 8f, radialScaleKnob);
        FloatKnobOrSlider(ref rotation, 0f, 1f, rotationKnob);
        FloatKnobOrSlider(ref innerRadius, 0f, 0.9f, innerRadiusKnob);
        RadioButtonsHorizontal(wrapMode);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    int WrapModeIndex()
    {
        if (wrapMode.IsSelected("Mirror")) return 1;
        if (wrapMode.IsSelected("Clamp")) return 2;
        return 0;
    }

    public override bool DoCalc()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        {
            if (outputTex != null)
                outputTex.Release();
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            return true;
        }

        if (tex.width != outputSize.x || tex.height != outputSize.y)
        {
            outputSize = new Vector2Int(tex.width, tex.height);
            InitializeRenderTexture();
        }

        if (repeatsKnob.connected()) angularRepeats = repeatsKnob.GetValue<float>();
        if (radialScaleKnob.connected()) radialScale = radialScaleKnob.GetValue<float>();
        if (rotationKnob.connected()) rotation = rotationKnob.GetValue<float>();
        if (innerRadiusKnob.connected()) innerRadius = innerRadiusKnob.GetValue<float>();

        tileShader.SetInt("oWidth", outputSize.x);
        tileShader.SetInt("oHeight", outputSize.y);
        tileShader.SetFloat("angularRepeats", angularRepeats);
        tileShader.SetFloat("radialScale", radialScale);
        tileShader.SetFloat("rotation", rotation);
        tileShader.SetFloat("innerRadius", Mathf.Clamp(innerRadius, 0f, 0.95f));
        tileShader.SetInt("wrapMode", WrapModeIndex());
        tileShader.SetTexture(kernelId, "InputTex", tex);
        tileShader.SetTexture(kernelId, "OutputTex", outputTex);
        int groupsX = Mathf.CeilToInt(outputSize.x / 16f);
        int groupsY = Mathf.CeilToInt(outputSize.y / 16f);
        tileShader.Dispatch(kernelId, groupsX, groupsY, 1);

        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
