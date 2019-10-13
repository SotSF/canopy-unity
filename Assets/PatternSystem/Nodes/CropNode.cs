using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using UnityEngine;

[Node(false, "Filter/Crop")]
public class CropNode : Node
{
    public const string ID = "cropNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Crop"; } }
    public override Vector2 DefaultSize { get { return new Vector2(200, 150); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture))]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Width", Direction.In, typeof(float))]
    public ValueConnectionKnob widthInputKnob;

    [ValueConnectionKnob("Height", Direction.In, typeof(float))]
    public ValueConnectionKnob heightInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader CropShader;
    private Vector4 HSV;
    public RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    public float width, height;

    private void Awake()
    {
        width = 75;
        height = 24;
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
        widthInputKnob.DisplayLayout();
        if (!widthInputKnob.connected())
        {
            width = RTEditorGUI.Slider(width, 1f, 1000f);
        }
        heightInputKnob.DisplayLayout();
        if (!heightInputKnob.connected())
        {
            height = RTEditorGUI.Slider(height, 1f, 1000f);
        }
        GUILayout.EndVertical();

        textureOutputKnob.DisplayLayout();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        Texture inputTex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || inputTex == null)
        { // Reset outputs if no texture is available
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            return true;
        }

        if (outputSize.x != (int)width || outputSize.y != (int)height)
        {
            outputSize = new Vector2Int((int)width, (int)height);
            InitializeRenderTexture();
        }

        Vector2 scale = new Vector2(1, 1);
        Vector2 offset = new Vector2(0,0);
        Graphics.Blit(inputTex, outputTex, scale, offset);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
