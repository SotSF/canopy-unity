using System;
using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using UnityEngine;
using UnityEngine.Video;

[Node(false, "Pattern/ChristmasTree")]
public class ChristmasTreeNode : TickingNode
{
    public const string ID = "christmasTreeNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "Christmas Tree"; } }
    private Vector2 _DefaultSize = new Vector2(250, 250);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("speed", Direction.In, "Float")]
    public ValueConnectionKnob speedKnob;

    private RenderTexture outputTex;
    private ComputeShader patternShader;
    private Vector2Int outputSize = new Vector2Int(128,128);
    private int patternKernel;
    private Texture2D noiseTex;
    private Texture2D treeTex;


    [ValueConnectionKnob("Turbulence itensity", Direction.In, "Float")]
    public ValueConnectionKnob turbIntensityKnob;

    [ValueConnectionKnob("Turbulence scale", Direction.In, "Float")]
    public ValueConnectionKnob turbScaleKnob;

    [ValueConnectionKnob("Tinsel thickness", Direction.In, "Float")]
    public ValueConnectionKnob tinselThicknessKnob;

    [ValueConnectionKnob("Tinsel amplitude", Direction.In, "Float")]
    public ValueConnectionKnob tinselAmplitudeKnob;

    [ValueConnectionKnob("Tinsel offset", Direction.In, "Float")]
    public ValueConnectionKnob tinselOffsetKnob;

    public float turbFactor = .25f;
    public float turbScale = 16;
    public float tinselThickness = 5;
    public float tinselAmplitude = 24;
    public float tinselOffset = 24;

    public TinselFunction tinselOne;

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
                float xCoord = x / noiseTex.width * turbScale;
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
        treeTex = new Texture2D(outputSize.x, outputSize.y);
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
        tinselThicknessKnob.DisplayLayout();
        if (!tinselThicknessKnob.connected())
        {
            tinselThickness = RTEditorGUI.Slider(tinselThickness, 1, 32, options: GUILayout.MaxWidth(120));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        tinselAmplitudeKnob.DisplayLayout();
        if (!tinselAmplitudeKnob.connected())
        {
            tinselAmplitude = RTEditorGUI.Slider(tinselAmplitude, 2, 32, options: GUILayout.MaxWidth(120));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        tinselOffsetKnob.DisplayLayout();
        if (!tinselOffsetKnob.connected())
        {
            tinselOffset = RTEditorGUI.Slider(tinselOffset, 0, 128, options: GUILayout.MaxWidth(120));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.MaxHeight(40));
        GUILayout.Box(noiseTex, GUILayout.MaxHeight(100));
        GUILayout.Box(treeTex, GUILayout.MaxHeight(100));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public struct TinselFunction
    {
        public float phase;
        public float amplitude;
        public float period;
        public float thickness;
        public float offset;

        public bool containsPoint(float x, float y)
        {
            float valueAtX = -amplitude * Mathf.Sqrt(1 - Mathf.Pow((x + phase) / period % 2 - 1, 2)) + offset;
            return Math.Abs(valueAtX - y) < (thickness / 2);
        }
    }

    private float Rescale(float value, float inRangeMin, float inRangeMax, float outRangeMin, float outRangeMax)
    {
        if (value <= inRangeMin)
            return outRangeMin;
        else if (value >= inRangeMax)
            return outRangeMax;
        return (outRangeMax - outRangeMin) * ((value - inRangeMin) / (inRangeMax - inRangeMin)) + outRangeMin;
    }

    private void DrawTree()
    {
        float tinselPulseFactor = Rescale(Mathf.Sin(Time.time / 10), -1, 1, 0.6f, 0.8f);
        for (int y = 0; y < noiseTex.height; y++)
        {
            for (int x = 0; x < noiseTex.width; x++)
            {
                if (tinselOne.containsPoint(x, y))
                {
                    treeTex.SetPixel(x, y, Color.red);
                } else if (noiseTex.GetPixel(x,y).r > turbFactor)
                {
                    treeTex.SetPixel(x, y, Color.white);
                } else
                {
                    treeTex.SetPixel(x, y, Color.green);
                }
            }
        }
        treeTex.Apply();
    }

    public override bool Calculate()
    {
        tinselOne.phase = Time.time/10;
        tinselOne.amplitude = tinselAmplitude;
        tinselOne.thickness = tinselThickness;
        tinselOne.offset = tinselOffset;
        tinselOne.period = tinselOne.amplitude * 1.25f;

        turbScale = turbScaleKnob.connected() ? turbScaleKnob.GetValue<float>() : turbScale;
        if (turbScale != oldScale)
        {
            oldScale = turbScale;
            GenerateNoiseTex();
            patternShader.SetTexture(patternKernel, "noiseTex", noiseTex);
        }
        turbFactor = turbIntensityKnob.connected() ? turbIntensityKnob.GetValue<float>() : turbFactor;

        patternShader.SetInt("width", outputTex.width);
        patternShader.SetInt("height", outputTex.height);
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        uint tx, ty, tz;
        //patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        //patternShader.Dispatch(patternKernel, Mathf.CeilToInt(outputTex.width / tx), Mathf.CeilToInt(outputTex.height / ty), 1);
        DrawTree();
        textureOutputKnob.SetValue(treeTex);
        return true;
    }
}