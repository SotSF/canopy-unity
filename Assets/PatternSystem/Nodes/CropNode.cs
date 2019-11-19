using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using UnityEngine;

[Node(false, "Filter/CropTileScale")]
public class CropNode : Node
{
    public const string ID = "cropTileScale";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Crop Tile Scale"; } }
    public override Vector2 DefaultSize { get { return new Vector2(200, 150); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Width", Direction.In, typeof(float))]
    public ValueConnectionKnob widthInputKnob;
    public float width = 75;

    [ValueConnectionKnob("Height", Direction.In, typeof(float))]
    public ValueConnectionKnob heightInputKnob;
    public float height = 96;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture),NodeSide.Bottom, 180)]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader CropShader;
    private Vector4 HSV;
    public RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    private bool scale;
    private bool tile;
    private bool mirror;
    private int tileKernel;
    private int mirrorKernel;
    private int cropScaleKernel;

    private void Awake()
    {
        CropShader = Resources.Load<ComputeShader>("FilterShaders/CropScaleTileFilter");
        // tileKernel = CropShader.FindKernel("TileKernel");
        mirrorKernel = CropShader.FindKernel("MirrorKernel");
        cropScaleKernel = CropShader.FindKernel("CropScaleKernel");
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        textureInputKnob.DisplayLayout();

        GUILayout.BeginHorizontal();
        widthInputKnob.DisplayLayout();
        if (!widthInputKnob.connected())
        {
            width = RTEditorGUI.Slider(width, 1f, 1000f);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        heightInputKnob.DisplayLayout();
        if (!heightInputKnob.connected())
        {
            height = RTEditorGUI.Slider(height, 1f, 1000f);
        }
        GUILayout.EndHorizontal();

        // Strategy for in size < out size: choose scale/tile/mirror, default is fill black/alpha
        // Strategy for out size < in size: default crop, allow scale
        GUILayout.BeginHorizontal();
        if (RTEditorGUI.Toggle(scale, new GUIContent("Scale", "Scale to given width/height"))){
            scale = true;
            tile = false;
            mirror = false;
        } else {
            scale = false;
        }
        if (RTEditorGUI.Toggle(tile, new GUIContent("Tile", "Tile texture onto larger canvas"))){
            tile = true;
            scale = false;
            mirror = false;
        } else {
            tile = false;
        }
        if (RTEditorGUI.Toggle(mirror, new GUIContent("Mirror", "Mirror texture onto larger canvas"))){
            mirror = true;
            scale = false;
            tile = false;
        } else {
            mirror = false;
        }
        GUILayout.EndHorizontal();
        GUILayout.Box(outputTex, GUILayout.MaxHeight(64));
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();

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
        int kernelID = 0;
        if (tile){
            kernelID = tileKernel;
        } else if (mirror){
            kernelID = mirrorKernel;
        } else {
            kernelID = cropScaleKernel;
        }
        if (outputSize.x != (int)width || outputSize.y != (int)height)
        {
            outputSize = new Vector2Int((int)width, (int)height);
            InitializeRenderTexture();
        }
        CropShader.SetTexture(kernelID, "InputTex", inputTex);
        CropShader.SetTexture(kernelID, "OutputTex", outputTex);
        CropShader.SetInt("iWidth", inputTex.width);
        CropShader.SetInt("iHeight", inputTex.height);
        CropShader.SetInt("oWidth", outputTex.width);
        CropShader.SetInt("oHeight", outputTex.height);
        CropShader.SetBool("applyScale", scale);
        var threadGroupX = Mathf.CeilToInt(outputTex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(outputTex.height / 16.0f);
        CropShader.Dispatch(kernelID, threadGroupX, threadGroupY, 1);
        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
