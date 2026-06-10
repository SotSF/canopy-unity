using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using UnityEngine;


// Mirrors the input texture across the X or Y axis via a scale/offset blit, then
// passes the result downstream. Useful for correcting orientation/handedness in the
// chain (e.g. a left/right inversion in the physical output) within the canvas context.
[Node(false, "Filter/Mirror")]
public class MirrorFilterNode : TextureSynthNode
{
    public const string ID = "mirrorFilterNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Mirror"; } }
    private Vector2 _DefaultSize = new Vector2(150, 120);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("InTex", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("OutTex", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    public RadioButtonSet axisMode;

    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    public override void DoInit()
    {
        if (axisMode == null || axisMode.names.Count == 0)
        {
            axisMode = new RadioButtonSet(0, "X", "Y");
        }
    }

    private void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("Mirror axis");
        RadioButtons(axisMode);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
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

        var inputSize = new Vector2Int(tex.width, tex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }

        // Flip across the chosen axis: negate that axis' scale and offset by 1 to keep
        // the sampled UVs in the [0,1] range.
        Vector2 scale = axisMode.IsSelected("Y") ? new Vector2(1, -1) : new Vector2(-1, 1);
        Vector2 offset = axisMode.IsSelected("Y") ? new Vector2(0, 1) : new Vector2(1, 0);
        Graphics.Blit(tex, outputTex, scale, offset);

        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
