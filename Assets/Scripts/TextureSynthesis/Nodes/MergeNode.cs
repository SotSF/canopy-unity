
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using UnityEngine;

[Node(false, "Filter/Merge")]
public class MergeNode : Node
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

    
    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    private float crossfader = 0;
    public RenderTexture outputTex;

    
    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/MergeFilter");
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
        
        texLKnob.SetPosition(20);
        texRKnob.SetPosition(60);

        GUILayout.BeginVertical();
        
        crossfaderKnob.DisplayLayout();
        if (!crossfaderKnob.connected())
        {
            crossfader = RTEditorGUI.Slider(crossfader, 0, 1);
        } else
        {
            crossfader = crossfaderKnob.GetValue<float>();
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

        crossfader = crossfaderKnob.connected() ? crossfaderKnob.GetValue<float>(): crossfader;

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetFloat("crossfader", crossfader);
        patternShader.SetTexture(patternKernel, "texL", texL);
        patternShader.SetTexture(patternKernel, "texR", texR);
        patternShader.SetTexture(patternKernel, "outputTex", outputTex);

        uint tx,ty,tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
        outputTexKnob.SetValue(outputTex);

        return true;
    }
}
