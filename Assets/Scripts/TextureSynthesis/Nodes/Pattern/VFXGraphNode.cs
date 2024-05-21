
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/VFXGraph")]
public class VFXGraphNode: TickingNode
{
    public override string GetID => "VFXGraph";
    public override string Title { get { return "VFXGraph"; } }
    private Vector2 _DefaultSize = new Vector2(200, 200);

    public override Vector2 DefaultSize => _DefaultSize;

    
    [ValueConnectionKnob("emissionRate", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob emissionRateKnob;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private Vector2Int outputSize = Vector2Int.zero;
    private float emissionRate = 200;
    //private float speedFactor = 1;
    private RenderTexture outputTex;

    private Transform vfxPrefab;
    private Camera cam;

    public void Awake()
    {
        vfxPrefab = Resources.Load<Transform>("Prefabs/");
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
        GUILayout.BeginVertical();
        
        emissionRateKnob.DisplayLayout();
        if (!emissionRateKnob.connected())
        {
            emissionRate = RTEditorGUI.Slider(emissionRate, 0, 1000);
        } else
        {
            emissionRate = emissionRateKnob.GetValue<float>();
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
        emissionRate = emissionRateKnob.connected() ? emissionRateKnob.GetValue<float>(): emissionRate;

        outputTexKnob.SetValue(outputTex);
        return true;
    }
}
