using System;
using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;
using UnityEngine.Video;

[Node(false, "Pattern/VortexGenerator")]
public class VortexGeneratorNode : Node
{
    public const string ID = "vortexGeneratorNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "VortexVelocity"; } }
    private Vector2 _DefaultSize = new Vector2(250, 250);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Offset", Direction.In, "Float")]
    public ValueConnectionKnob offsetKnob;

    [ValueConnectionKnob("Turbulence itensity", Direction.In, "Float")]
    public ValueConnectionKnob turbIntensityKnob;

    [ValueConnectionKnob("Turbulence scale", Direction.In, "Float")]
    public ValueConnectionKnob turbScaleKnob;

    public float normalAngleOffset = .25f;
    public float turbFactor = .25f;
    public float turbScale = 16;
    private RenderTexture outputTex;
    private ComputeShader patternShader;
    private Vector2Int outputSize = new Vector2Int(128,128);
    private int patternKernel;
    private Texture2D noiseTex;

    private void Awake()
    {
        patternShader = Resources.Load<ComputeShader>("NodeShaders/VortexGeneratorPattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
        InitializeRenderTexture();
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        GenerateNoiseTex();
        patternShader.SetTexture(patternKernel, "noiseTex", noiseTex);
    }

    private void GenerateNoiseTex()
    {
        if (noiseTex != null)
        {
            Destroy(noiseTex);
        }
        noiseTex = new Texture2D(outputSize.x, outputSize.y);
        // For each pixel in the texture...
        float y = 0.0F;
        Color32[] pixels = new Color32[noiseTex.width * noiseTex.height];
        while (y < noiseTex.height)
        {
            float x = 0.0F;
            while (x < noiseTex.width)
            {
                float xCoord =  x / noiseTex.width * turbScale;
                float yCoord = y / noiseTex.height * turbScale;
                float sample = Mathf.PerlinNoise(xCoord, yCoord);
                pixels[(int)y * noiseTex.width + (int)x] = new Color(sample, sample, sample);
                x++;
            }
            y++;
        }

        // Copy the pixel data to the texture and load it into the GPU.
        noiseTex.SetPixels32(pixels);
        noiseTex.Apply();
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    float oldScale = 0;
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        //GUILayout.Label(new GUIContent("Turbulence factor", "The offset of the normal angle per point"));
        turbIntensityKnob.DisplayLayout();
        if (!turbIntensityKnob.connected())
        {
            turbFactor = RTEditorGUI.Slider(turbFactor, 0, 1, options: GUILayout.MaxWidth(120));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        turbScaleKnob.DisplayLayout();
        //GUILayout.Label(new GUIContent("Turbulence scale", "The offset of the normal angle per point"));
        if (!turbScaleKnob.connected())
        {
            turbScale = RTEditorGUI.Slider(turbScale, 2, 64, options: GUILayout.MaxWidth(120));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        offsetKnob.DisplayLayout();
        //GUILayout.Label(new GUIContent("Angle offset", "The offset of the normal angle per point"));
        if (!offsetKnob.connected())
        {
            normalAngleOffset = RTEditorGUI.Slider(normalAngleOffset, 0, 1, options: GUILayout.MaxWidth(120));
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        GUILayout.Box(noiseTex, GUILayout.MaxHeight(100));
        GUILayout.Box(outputTex, GUILayout.MaxHeight(100));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        turbScale = turbScaleKnob.connected() ? turbScaleKnob.GetValue<float>() : turbScale;
        if (turbScale != oldScale)
        {
            oldScale = turbScale;
            GenerateNoiseTex();
            patternShader.SetTexture(patternKernel, "noiseTex", noiseTex);
        }
        normalAngleOffset = offsetKnob.connected() ? offsetKnob.GetValue<float>() : normalAngleOffset;
        turbFactor = turbIntensityKnob.connected() ? turbIntensityKnob.GetValue<float>() : turbFactor;
        patternShader.SetInt("width", outputTex.width);
        patternShader.SetInt("height", outputTex.height);
        patternShader.SetFloat("normalAngleOffset", normalAngleOffset);
        patternShader.SetFloat("noiseFactor", turbFactor);
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        patternShader.Dispatch(patternKernel, Mathf.CeilToInt(outputTex.width / tx), Mathf.CeilToInt(outputTex.height / ty), 1);
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}