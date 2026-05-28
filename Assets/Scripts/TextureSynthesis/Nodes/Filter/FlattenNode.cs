using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;

using SecretFire.TextureSynth;

using UnityEngine;


// Bakes a (possibly translucent) input texture down to opaque RGB by compositing
// it over a black background (rgb *= alpha), then forcing alpha to 1. Insert this
// before outputs that discard alpha (e.g. CanopyArtnet) so that opacity from nodes
// like Merge is reflected as actual brightness on the physical hardware.
[Node(false, "Filter/Flatten")]
public class FlattenNode : TextureSynthNode
{
    public const string ID = "flattenNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Flatten"; } }
    private Vector2 _DefaultSize = new Vector2(150, 120);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("InTex", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("OutTex", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader flattenShader;
    private int kernelId;
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2Int inputSize;

    public override void DoInit()
    {
        flattenShader = Resources.Load<ComputeShader>("NodeShaders/FlattenFilter");
        kernelId = flattenShader.FindKernel("CSMain");
        inputSize = new Vector2Int(0, 0);
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
        GUILayout.Label("Bake alpha over black");
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
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

        flattenShader.SetTexture(kernelId, "InputTex", tex);
        flattenShader.SetTexture(kernelId, "OutputTex", outputTex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        flattenShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
