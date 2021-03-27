
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Filter/Merge")]
public class MergeNode : TextureSynthNode
{
    public override string GetID => "MergeNode";
    public override string Title { get { return "Merge"; } }

    public override Vector2 DefaultSize { get { return new Vector2(200, 200); } }

    
    [ValueConnectionKnob("texL", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob texLKnob;

    [ValueConnectionKnob("texR", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob texRKnob;

    [ValueConnectionKnob("crossfader", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob crossfaderKnob;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    
    public float crossfader = 0;

    private ComputeShader patternShader;
    private int layerKernel;
    private int fadeKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture outputTex;
    public RadioButtonSet mergeModeSelection;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/MergeFilter");
        layerKernel = patternShader.FindKernel("LayerKernel");
        fadeKernel = patternShader.FindKernel("FadeKernel");
        if (mergeModeSelection == null || mergeModeSelection.names.Count == 0)
        {
            mergeModeSelection = new RadioButtonSet(0, "Simple", "Layers");
        }
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
        
        texLKnob.SetPosition(20);
        texRKnob.SetPosition(60);

        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        RadioButtons(mergeModeSelection);
        GUILayout.EndHorizontal();

        if (mergeModeSelection.Selected == "Simple")
        {
            crossfaderKnob.DisplayLayout();
            if (!crossfaderKnob.connected())
            {
                crossfader = RTEditorGUI.Slider(crossfader, 0, 1);
            }
            else
            {
                crossfader = crossfaderKnob.GetValue<float>();
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        GUILayout.EndVertical();
        
        outputTexKnob.SetPosition(180);

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {

        Texture texL = texLKnob.GetValue<Texture>();
        if (!texLKnob.connected () || texL == null)
        {
            outputTexKnob.ResetValue();
            outputSize = Vector2Int.zero;
            
            if (outputTex != null)
                outputTex.Release();
            return true;
        }

        Texture texR = texRKnob.GetValue<Texture>();
        if (!texRKnob.connected () || texR == null)
        {
            outputTexKnob.ResetValue();
            outputSize = Vector2Int.zero;
            
            if (outputTex != null)
                outputTex.Release();
            return true;
        }
        
        var inputSize = new Vector2Int(texL.width, texL.height);
        if (inputSize != outputSize){
            outputSize = inputSize;
            InitializeRenderTexture();
        }

        

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        int kernel = 0;
        switch (mergeModeSelection.Selected)
        {
            case "Simple":
                crossfader = crossfaderKnob.connected() ? crossfaderKnob.GetValue<float>() : crossfader;
                patternShader.SetFloat("crossfader", crossfader);
                kernel = fadeKernel;
                break;
            case "Layers":
                kernel = layerKernel;
                break;
        }

        patternShader.SetTexture(kernel, "texL", texL);
        patternShader.SetTexture(kernel, "texR", texR);
        patternShader.SetTexture(kernel, "outputTex", outputTex);

        uint tx,ty,tz;
        patternShader.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        outputTexKnob.SetValue(outputTex);

        return true;
    }
}
