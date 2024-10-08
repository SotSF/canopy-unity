using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using UnityEngine;


[Node(false, "Test/StripTestNode")]
public class StripTestNode : TickingNode
{
    public const string ID = "stripTestNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "StripTest"; } }
    private Vector2 _DefaultSize = new Vector2(150, 180); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Texture", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;


    private ComputeShader StripTestShader;
    private int kernelId;
    private RenderTexture outputTex;
    private Vector2Int outputSize = new Vector2Int(151,96);

    public bool testPort = false;
    public int portId = 0;

    public override void DoInit()
    {
        StripTestShader = Resources.Load<ComputeShader>("NodeShaders/StripTestPattern");
        kernelId = StripTestShader.FindKernel("CSMain");
        InitializeRenderTexture();
        StripTestShader.SetTexture(kernelId, "OutputTex", outputTex);
        
    }

    int strip = 0;
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
    bool pulse = false;
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Current strip id: "+strip);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Plus"))
        {
            strip = (strip +1) % 96;
        }
        if (GUILayout.Button("Minus"))
        {
            strip = (strip - 1 < 0) ? 95 : (strip - 1) % 96;
        }
        GUILayout.EndHorizontal();
        if (testPort = RTEditorGUI.Toggle(testPort, "Test a port"))
        {
            portId = RTEditorGUI.IntSlider("Test Port ID", portId, 1, 16);
        }
        pulse = RTEditorGUI.Toggle(pulse, "Do pulse");
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        //Execute HSV compute shader here
        var threadGroupX = Mathf.CeilToInt(outputSize.x / 16.0f);
        var threadGroupY = Mathf.CeilToInt(outputSize.y/ 16.0f);
        StripTestShader.SetInt("port", testPort ? portId : 0);
        StripTestShader.SetInt("strip", strip);
        StripTestShader.SetBool("pulse", pulse);
        StripTestShader.SetFloat("time", Time.time);
        StripTestShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}