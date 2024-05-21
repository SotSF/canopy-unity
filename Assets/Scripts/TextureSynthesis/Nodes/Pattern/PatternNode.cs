using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;


abstract public class PatternNode : TickingNode
{
    private Vector2 _DefaultSize = new Vector2(150, 100);

    public override Vector2 DefaultSize => _DefaultSize;

     [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;


    private ComputeShader patternShader;
    private int patternKernel;

    private RenderTexture outputTex;

    private Vector2Int outputSize = Vector2Int.zero;

    private void Awake()
    {
        patternShader = Resources.Load<ComputeShader>(string.Format("PatternShaders/{0}Pattern}", GetID));
        patternKernel = patternShader.FindKernel("PatternKernel");
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
        //GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        // Loop over control list dynamically? Or delegate to subclass?

        // Draw output texture
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(75), GUILayout.MaxHeight(96));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        //GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public void BindAndExecute(){
        patternShader.SetInt("width", outputTex.width);
        patternShader.SetInt("height", outputTex.height);
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        uint tx,ty,tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(outputTex.width / tx);
        var threadGroupY = Mathf.CeilToInt(outputTex.height / ty);
        patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
    }

    public override bool Calculate()
    {
        // Bind the
        BindAndExecute();
        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
