using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Text;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

[Node(false, "Pattern/ReactionDiffusion")]
public class ReactionDiffusion : TickingNode
{
    public override string GetID => "ReactionDiffusionNode";
    public override string Title { get { return "ReactionDiffusion"; } }
    private Vector2 _DefaultSize = new Vector2(680, 240);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("aIn", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob aInputKnob;

    [ValueConnectionKnob("bIn", Direction.In, typeof(Texture), NodeSide.Top, 60)]
    public ValueConnectionKnob bInputKnob;

    [ValueConnectionKnob("feedRate", Direction.In, typeof(float))]
    public ValueConnectionKnob feedRateKnob;
    public float feedRate = 0.7f;

    [ValueConnectionKnob("killRate", Direction.In, typeof(float))]
    public ValueConnectionKnob killRateKnob;
    public float killRate = 0.02f;

    [ValueConnectionKnob("aDiffusionRate", Direction.In, typeof(float))]
    public ValueConnectionKnob aDiffusionRateKnob;
    public float aDiffusionRate = 1;

    [ValueConnectionKnob("bDiffusionRate", Direction.In, typeof(float))]
    public ValueConnectionKnob bDiffusionRateKnob;
    public float bDiffusionRate = 0.5f;

    [ValueConnectionKnob("timeMultiplier", Direction.In, typeof(float))]
    public ValueConnectionKnob timeMultiplierKnob;
    public float timeMultiplier = 1;


    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    public float timestep;

    public bool useBoundaries = true;
    public bool continuousA = false;
    public bool continuousB = false;
    public bool running = false;

    private ComputeShader reactionDiffusionShader;
    private int reactionDiffusionKernel;
    private int addInputKernel;

    // Uses two channels (R,G) to store the 2vector field U(x,y)
    private RenderTexture outputTex;
    private RenderTexture aField;
    private RenderTexture bField;
    private RenderTexture scaledBuffer;

    private Vector2Int outputSize = new Vector2Int(256, 256);
    private RenderTextureFormat rtFmt = RenderTextureFormat.RFloat;

    private RenderTexture MakeRenderTexture()
    {
        var tex = new RenderTexture(outputSize.x, outputSize.y, 0, rtFmt);
        tex.useMipMap = false;
        tex.autoGenerateMips = false;
        tex.enableRandomWrite = true;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Create();
        return tex;
    }

    private void InitializeRenderTextures()
    {
        outputTex = MakeRenderTexture();
        aField = MakeRenderTexture();
        bField = MakeRenderTexture();
        scaledBuffer = MakeRenderTexture();
    }

    private void ClearRenderTextures()
    {
        RenderTexture.active = aField;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = bField;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = outputTex;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = scaledBuffer;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;
    }

    private void OnDestroy()
    {
        RenderTexture[] textures = { outputTex, aField, bField, scaledBuffer };
        foreach (var t in textures)
        {
            if (t != null)
                t.Release();
        }
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    public override void DoInit()
    {
        reactionDiffusionShader = Resources.Load<ComputeShader>("NodeShaders/ReactionDiffusionPattern");
        reactionDiffusionKernel = reactionDiffusionShader.FindKernel("reactionDiffusion");
        addInputKernel = reactionDiffusionShader.FindKernel("addInput");
        reactionDiffusionShader.SetInt("width", outputSize.x);
        reactionDiffusionShader.SetInt("height", outputSize.y);
        InitializeRenderTextures();
        ClearRenderTextures();
    }
    
    bool clicked = false;
    GUIContent continuousALabel = new GUIContent("Continuous A", "Add A chemical texture every frame");
    GUIContent continuousBLabel = new GUIContent("Continuous B", "Add B chemical every frame");
    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        aInputKnob.SetPosition(140);

        // Top row simulation control buttons
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref timeMultiplier, -1, 2, timeMultiplierKnob);
        FloatKnobOrSlider(ref feedRate, 0, 1, feedRateKnob);
        FloatKnobOrSlider(ref killRate, 0, 1, killRateKnob);
        FloatKnobOrSlider(ref aDiffusionRate, 0, 1, aDiffusionRateKnob);
        FloatKnobOrSlider(ref bDiffusionRate, 0, 1, bDiffusionRateKnob);
        string cmd = running ? "Stop" : "Run";
        clicked =GUILayout.Button(cmd);
        if (clicked)
        {
            running = !running;
        }
        if (GUILayout.Button("Apply A")) 
        { 
            ApplyInput(true);
        }
        if (GUILayout.Button("Apply B"))
        {
            ApplyInput(false);
        }
        if (GUILayout.Button("Reset"))
        {
            ClearRenderTextures();
            running = false;
        }
        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        // parameters / buttons
        GUILayout.BeginHorizontal();

        continuousA = RTEditorGUI.Toggle(continuousA, continuousALabel);
        continuousB = RTEditorGUI.Toggle(continuousB, continuousBLabel);

        GUILayout.EndHorizontal();
        // Texture output
        GUILayout.BeginHorizontal();

        GUILayout.Box(aField, GUILayout.MaxWidth(128), GUILayout.MaxHeight(200));
        GUILayout.Box(bField, GUILayout.MaxWidth(128), GUILayout.MaxHeight(200));
        GUILayout.Box(outputTex, GUILayout.MaxWidth(128), GUILayout.MaxHeight(200));
        GUILayout.EndHorizontal();
        textureOutputKnob.SetPosition(DefaultSize.x - 40);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void ApplyInput(bool aOrB, float multiplier = 1)
    {
        Texture input = aOrB ? aInputKnob.GetValue<Texture>() : bInputKnob.GetValue<Texture>();
        if (input != null && input.width > 0)
        {
            Graphics.Blit(input, scaledBuffer);
            reactionDiffusionShader.SetFloat("inputMultiplier", multiplier);
            reactionDiffusionShader.SetTexture(addInputKernel, "inputField", scaledBuffer);
            reactionDiffusionShader.SetTexture(addInputKernel, "aField", aOrB ? aField : bField);
            reactionDiffusionShader.SetTexture(addInputKernel, "bField", aOrB ? aField : bField);
            var groupSizeX = Mathf.CeilToInt((outputSize.x) / 16f);
            var groupSizeY = Mathf.CeilToInt((outputSize.y) / 16f);
            reactionDiffusionShader.Dispatch(addInputKernel, groupSizeX, groupSizeY, 1);
        }
    }
    private void SimulateReaction()
    {
        reactionDiffusionShader.SetFloat("timestep", timestep*timeMultiplier);
        reactionDiffusionShader.SetInt("width", outputSize.x);
        reactionDiffusionShader.SetInt("height", outputSize.y);
        reactionDiffusionShader.SetFloat("diffusionRateA", aDiffusionRate);
        reactionDiffusionShader.SetFloat("diffusionRateB", bDiffusionRate);
        reactionDiffusionShader.SetFloat("killRate", killRate);
        reactionDiffusionShader.SetFloat("feedRate", feedRate);
        reactionDiffusionShader.SetTexture(reactionDiffusionKernel, "aField", aField);
        reactionDiffusionShader.SetTexture(reactionDiffusionKernel, "bField", bField);
        reactionDiffusionShader.SetTexture(reactionDiffusionKernel, "inputField", scaledBuffer);
        var groupSizeX = Mathf.CeilToInt((outputSize.x) / 16f);
        var groupSizeY = Mathf.CeilToInt((outputSize.y) / 16f);
        reactionDiffusionShader.Dispatch(reactionDiffusionKernel, groupSizeX, groupSizeY, 1);
        Graphics.Blit(bField, outputTex);
    }

    
    float lastStep = 0;
    public override bool DoCalc()
    {
        if (timeMultiplierKnob.connected())
        {
            timeMultiplier = timeMultiplierKnob.GetValue<float>();
        }
        if (running && Time.time - lastStep > 1/60f)
        {
            if (continuousA)
            {
                ApplyInput(true, Time.deltaTime);
            }
            if (continuousB)
            {
                ApplyInput(false, Time.deltaTime);
            }
            if (clicked)
            {
                timestep = Time.deltaTime;
            } else
            {
                timestep = Time.time - lastStep;
            }
            lastStep = Time.time;
            SimulateReaction();
        }
        if (clicked)
        {
            clicked = false;
        }
        textureOutputKnob.SetValue<Texture>(outputTex);
        return true;
    }
}
