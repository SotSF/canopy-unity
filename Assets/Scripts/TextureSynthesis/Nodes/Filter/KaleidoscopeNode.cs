using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Filter/Kaleidoscope")]
public class KaleidoscopeNode : TextureSynthNode
{
    public const string ID = "kaleidoscopeNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Kaleidoscope"; } }
    private Vector2 _DefaultSize =new Vector2(150, 150);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Kaleidoscope", Direction.In, typeof(int))]
    public ValueConnectionKnob reflectionsInputKnob;

    private ComputeShader KaleidoscopeShader;
    private int kernelId;
    private Vector4 HSV;
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;
    public int yReflections;

    public int reflections = 4;
    public int previousReflections = 4;

    public override void DoInit()
    {
        KaleidoscopeShader = Resources.Load<ComputeShader>("NodeShaders/KaleidoscopeFilter");
        kernelId = KaleidoscopeShader.FindKernel("CSMain");
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        textureInputKnob.DisplayLayout();

        reflectionsInputKnob.DisplayLayout(new GUIContent("Reflections", "The number of reflections"));
        if (!reflectionsInputKnob.connected())
        {
            reflections = RTEditorGUI.IntSlider(reflections, 1, 10);
        }
        GUILayout.EndVertical();

        textureOutputKnob.DisplayLayout();

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
            outputSize = Vector2Int.zero;
            return true;
        }

        if (outputSize.x == 0 || outputSize.y == 0 || reflections != previousReflections)
        {
            outputSize = new Vector2Int(tex.width, tex.height * reflections);
            previousReflections = reflections;
            InitializeRenderTexture();
            Debug.Log("tex.height");
            Debug.Log(tex.height);

        }

        //Execute compute shader
        KaleidoscopeShader.SetInt("width", tex.width);
        KaleidoscopeShader.SetInt("height", tex.height);
        KaleidoscopeShader.SetTexture(kernelId, "InputTex", tex);
        KaleidoscopeShader.SetTexture(kernelId, "OutputTex", outputTex);
        var threadGroupX = Mathf.CeilToInt(outputTex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(outputTex.height / 16.0f);
        KaleidoscopeShader.Dispatch(kernelId, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
