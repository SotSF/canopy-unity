using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;


abstract public class PatternNode : TickingNode
{
    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

     [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;


    private ComputeShader patternShader;
    private int patternKernel;

    public RenderTexture outputTex;

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
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
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

    public override bool Calculate()
    {
        patternShader.SetInt("width", outputTex.width);
        patternShader.SetInt("height", outputTex.height);
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(outputTex.width);
        var threadGroupY = Mathf.CeilToInt(outputTex.height / 16.0f);
        patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}