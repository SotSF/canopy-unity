using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Renders a Jet[] (from JetInstance / JetRadialArray / JetArrayCat) into the
/// velocity + dye texture pair consumed by the fluid sim. The velocity output
/// encodes force as HSV (hue = angle, value = magnitude); the dye output
/// places each jet's color at its nozzle. An optional dye texture input is
/// sampled through the dye spots instead of (or tinted by) the jet colors.
/// </summary>
[Node(false, "Pattern/FluidJetGenerator")]
public class FluidJetGeneratorNode : TickingNode
{
    public const string ID = "fluidJetGeneratorNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "JetRenderer"; } }
    private Vector2 _DefaultSize = new Vector2(250, 200);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Jets", Direction.In, typeof(Jet[]), NodeSide.Left, 20)]
    public ValueConnectionKnob jetsInputKnob;

    [ValueConnectionKnob("Dye texture", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob dyeTexInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Dye", Direction.Out, typeof(Texture), NodeSide.Bottom, 120)]
    public ValueConnectionKnob dyeOutputKnob;

    // Must match MAX_JETS in FluidJetGeneratorPattern.compute
    private const int maxJets = 128;
    private Vector4[] jetPosDir = new Vector4[maxJets];
    private Vector4[] jetShape = new Vector4[maxJets];
    private Vector4[] jetColor = new Vector4[maxJets];

    private int activeJets = 0;

    private ComputeShader patternShader;
    private int patternKernel;
    private int dyeKernel;
    private Vector2Int outputSize = new Vector2Int(256, 256);
    private RenderTexture outputTex;
    private RenderTexture dyeTex;

    public override void DoInit()
    {
        patternShader = Resources.Load<ComputeShader>("NodeShaders/FluidJetGeneratorPattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
        dyeKernel = patternShader.FindKernel("DyeKernel");
        InitializeRenderTextures();
    }

    private void InitializeRenderTextures()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        if (dyeTex != null)
        {
            dyeTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
        dyeTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        dyeTex.enableRandomWrite = true;
        dyeTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(30));
        jetsInputKnob.DisplayLayout(new GUIContent("Jets", "Jet[] array to render"));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(30));
        dyeTexInputKnob.DisplayLayout(new GUIContent("Dye texture", "Optional: sampled through the dye spots, tinted by jet colors"));
        GUILayout.EndHorizontal();

        GUILayout.Label(activeJets + " jets" + (activeJets >= maxJets ? " (capped at " + maxJets + ")" : ""));

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(96), GUILayout.MaxHeight(96));
        GUILayout.Box(dyeTex, GUILayout.MaxWidth(96), GUILayout.MaxHeight(96));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        Jet[] jets = jetsInputKnob.connected() ? jetsInputKnob.GetValue<Jet[]>() : null;
        activeJets = jets == null ? 0 : Mathf.Min(jets.Length, maxJets);
        for (int i = 0; i < activeJets; i++)
        {
            Jet jet = jets[i];
            jetPosDir[i] = new Vector4(jet.position.x, jet.position.y, jet.angle, jet.intensity);
            jetShape[i] = new Vector4(jet.width, jet.reach, jet.spread, 0);
            jetColor[i] = jet.color;
        }

        Texture dyeSource = dyeTexInputKnob.connected() ? dyeTexInputKnob.GetValue<Texture>() : null;

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetInt("jetCount", activeJets);
        patternShader.SetVectorArray("jetPosDir", jetPosDir);
        patternShader.SetVectorArray("jetShape", jetShape);
        patternShader.SetVectorArray("jetColor", jetColor);
        patternShader.SetInt("useDyeTexture", dyeSource != null ? 1 : 0);
        patternShader.SetTexture(dyeKernel, "DyeSourceTex", dyeSource != null ? dyeSource : Texture2D.whiteTexture);
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        patternShader.SetTexture(dyeKernel, "DyeTex", dyeTex);

        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
        patternShader.Dispatch(dyeKernel, threadGroupX, threadGroupY, 1);

        textureOutputKnob.SetValue(outputTex);
        dyeOutputKnob.SetValue(dyeTex);
        return true;
    }
}
