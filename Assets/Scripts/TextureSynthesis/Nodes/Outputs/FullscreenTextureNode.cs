
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Output/FullscreenTexture")]
public class FullscreenTextureNode : TextureSynthNode
{
    public override string GetID => "FullscreenTexture";
    public override string Title { get { return "FullscreenTexture"; } }
    private Vector2 _DefaultSize = new Vector2(200, 150);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    private int width = 1920;
    private int height = 1080;
    private Vector2 outputSize;

    private Texture inputTex;
    private void Awake(){
        //patternShader = Resources.Load<ComputeShader>("NodeShaders/ChromaKeyFilter");
        //patternKernel = patternShader.FindKernel("PatternKernel");
    }

    public override void NodeGUI()
    {
        inputTexKnob.SetPosition(20);

        GUILayout.BeginVertical();
        GUILayout.BeginVertical();
        width = RTEditorGUI.IntField("Width", width);
        height = RTEditorGUI.IntField("Height", height);
        GUILayout.EndVertical();
        // Bottom row: output image
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(inputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        if (inputTexKnob.connected())
        {
            inputTex = inputTexKnob.GetValue<Texture>();
        }
        if (inputTex != null)
        {
            Vector2 size = new Vector2(width, height);
            if (size != outputSize)
            {
                Debug.Log("Setting size to " + size);
                FullscreenOutput.instance.SetOutputSize(size);
                outputSize = size;
            }
            if (!FullscreenOutput.isAttached)
            {
                FullscreenOutput.instance.AttachTexture(inputTex);
            }
        }
        return true;
    }
}
