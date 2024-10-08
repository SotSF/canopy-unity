using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;

using SecretFire.TextureSynth;

using UnityEngine;


[Node(false, "Filter/TransposeNode")]
public class TransposeNode : TextureSynthNode
{
    public const string ID = "transposeNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Transpose"; } }

    private Vector2 _DefaultSize =new Vector2(150, 120);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Texture", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Texture", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;


    private ComputeShader TransposeShader;
    private int kernelId;
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    public override void DoInit()
    {
        TransposeShader = Resources.Load<ComputeShader>("NodeShaders/TransposeFilter");
        kernelId = TransposeShader.FindKernel("CSMain");
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
        textureOutputKnob.DisplayLayout();
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

        var inputSize = new Vector2Int(tex.height, tex.width);
        if (inputSize != outputSize)
        {
            outputSize = new Vector2Int(inputSize.x, inputSize.y);
            InitializeRenderTexture();
        }
        //Execute HSV compute shader here
        TransposeShader.SetTexture(kernelId, "OutputTex", outputTex);
        TransposeShader.SetTexture(kernelId, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(outputSize.x / 16.0f);
        var threadGroupY = Mathf.CeilToInt(outputSize.y/ 16.0f);
        TransposeShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}