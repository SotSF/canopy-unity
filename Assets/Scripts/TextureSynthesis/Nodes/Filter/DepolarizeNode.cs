using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using SecretFire.TextureSynth;
using UnityEngine;


[Node(false, "Filter/Depolarize")]
public class DepolarizeNode : TextureSynthNode
{
    public const string ID = "depolarizeNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Depolarize"; } }
    private Vector2 _DefaultSize = new Vector2(150, 150);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader DepolarizeShader;
    private int kernelId;
    private RenderTexture outputTex;
    
    // Fixed output size for Visualization (square)
    private Vector2Int outputSize = new Vector2Int(512, 512);

    public override void DoInit()
    {
        DepolarizeShader = Resources.Load<ComputeShader>("NodeShaders/DepolarizeFilter");
        kernelId = DepolarizeShader.FindKernel("Depolarize");
        InitializeRenderTexture();
    }

    private void InitializeRenderTexture()
    {
        if (outputTex != null) outputTex.Release();
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(128), GUILayout.MaxHeight(128));
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        { // Reset outputs if no texture is available
            textureOutputKnob.ResetValue();
            return true;
        }

        if (outputTex == null || !outputTex.IsCreated())
        {
            InitializeRenderTexture();
        }

        //Execute compute shader
        DepolarizeShader.SetInt("width", outputSize.x);
        DepolarizeShader.SetInt("height", outputSize.y);
        DepolarizeShader.SetTexture(kernelId, "OutputTex", outputTex);
        DepolarizeShader.SetTexture(kernelId, "InputTex", tex);
        
        // Threads match the [numthreads(16, 16, 1)] in the shader
        var threadGroupX = Mathf.CeilToInt((float)outputSize.x / 16.0f);
        var threadGroupY = Mathf.CeilToInt((float)outputSize.y / 16.0f);
        DepolarizeShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
