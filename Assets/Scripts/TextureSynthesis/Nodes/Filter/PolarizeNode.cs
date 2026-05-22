using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using SecretFire.TextureSynth;
using UnityEngine;


[Node(false, "Filter/Polarize")]
public class PolarizeNode : TextureSynthNode
{
    public const string ID = "polarizeNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Polarize"; } }
    private Vector2 _DefaultSize =new Vector2(100, 100);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    private ComputeShader PolarizeShader;
    private int kernelId;
    private RenderTexture outputTex;
    
    // Fixed output size for Canopy
    private Vector2Int outputSize = new Vector2Int(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS);

    public override void DoInit()
    {
        PolarizeShader = Resources.Load<ComputeShader>("NodeShaders/PolarizeFilter");
        kernelId = PolarizeShader.FindKernel("Polarize");
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
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
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
        PolarizeShader.SetInt("width", tex.width);
        PolarizeShader.SetInt("height", tex.height);
        PolarizeShader.SetInt("NumPixels", Constants.PIXELS_PER_STRIP);
        PolarizeShader.SetInt("NumStrips", Constants.NUM_STRIPS);
        PolarizeShader.SetFloat("ApexRadius", Canopy.instance.ApexRadius);
        PolarizeShader.SetFloat("CanopyOuterRadius", Canopy.instance.CanopyOuterRadius);
        PolarizeShader.SetBuffer(kernelId, "RadialDistances", Canopy.instance.RadialDistanceBuffer);
        PolarizeShader.SetTexture(kernelId, "OutputTex", outputTex);
        PolarizeShader.SetTexture(kernelId, "InputTex", tex);
        
        // Threads match the [numthreads(25, 16, 1)] in the shader
        var threadGroupX = Mathf.CeilToInt((float)outputSize.x / 25.0f);
        var threadGroupY = Mathf.CeilToInt((float)outputSize.y / 16.0f);
        PolarizeShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}