using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;


[Node(false, "Filter/Pan")]
public class PanNode : TickingNode { 
    public const string ID = "panNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Pan/Offset"; } }
    private Vector2 _DefaultSize =new Vector2(200, 210); 

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Speed", Direction.In, typeof(float))]
    public ValueConnectionKnob in1Knob;

    [ValueConnectionKnob("Angle", Direction.In, typeof(float))]
    public ValueConnectionKnob in2Knob;

    [ValueConnectionKnob("Reset", Direction.In, typeof(bool))]
    public ValueConnectionKnob resetKnob;

    private ComputeShader panShader;
    private int bilinearMirrorKernel;
    private int bilinearRepeatKernel;
    private int bilinearClampKernel;
    private int pointMirrorKernel;
    private int pointRepeatKernel;
    private int pointClampKernel;
    private RenderTexture outputTex;

    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2 offset = Vector2.zero;

    public bool smoothTransitions = true;
    public bool mirror;
    public float in1, in2;

    public RadioButtonSet offsetMode;
    public RadioButtonSet sampleMode;

    public override void DoInit()
    {
        panShader = Resources.Load<ComputeShader>("NodeShaders/PanFilter");
        bilinearMirrorKernel = panShader.FindKernel("BilinearMirror");
        bilinearRepeatKernel = panShader.FindKernel("BilinearRepeat");
        bilinearClampKernel = panShader.FindKernel("BilinearClamp");
        pointMirrorKernel = panShader.FindKernel("PointMirror");
        pointRepeatKernel = panShader.FindKernel("PointRepeat");
        pointClampKernel = panShader.FindKernel("PointClamp");
        if (offsetMode == null || offsetMode.names == null || offsetMode.names.Count == 0)
        {
            offsetMode = new RadioButtonSet(0, "X/Y position", "X/Y speed", "Speed/angle");
        }
        if (sampleMode == null || sampleMode.names == null || sampleMode.names.Count == 0)
        {
            sampleMode = new RadioButtonSet(0, "Mirror", "Repeat", "Clamp");
        }
    }

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

    float in1min = 0, in1max = 0, in2min = 0, in2max = 0;
    GUIContent smoothLabel = new GUIContent("Smooth", "Whether the image panning should use bilinear filtering to produce smooth transitions");
    GUILayoutOption imgWidth = GUILayout.MaxWidth(64);
    GUILayoutOption imgHeight = GUILayout.MaxHeight(64);
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        //Top row - pan options, offset mode
        GUILayout.BeginHorizontal();
        GUILayout.Space(4);
        // Options - smooth/mirror
        GUILayout.BeginVertical();
        GUILayout.Label("Pan options");
        smoothTransitions = RTEditorGUI.Toggle(smoothTransitions, smoothLabel);
        //mirror = RTEditorGUI.Toggle(mirror, new GUIContent("Mirror", "Use mirror wraping at texture edges"));
        GUILayout.Label("Sample mode");
        RadioButtons(sampleMode);
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // Offset mode
        GUILayout.BeginVertical();
        GUILayout.Label("Offset mode");
        RadioButtons(offsetMode);
        GUILayout.Space(4);
        GUILayout.EndVertical();
        GUILayout.Space(4);
        GUILayout.EndHorizontal();

        // Middle row - Input knobs/sliders
        FloatKnobOrSlider(ref in1, in1min, in1max, in1Knob);
        FloatKnobOrSlider(ref in2, in2min, in2max, in2Knob);

        // Bottom row - reset button, tex view
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("Offset: ({0:0.0}, {1:0.0})", offset.x, offset.y));
        if (EventKnobOrButton("Reset", resetKnob))
        {
            offset = Vector2.zero;
            in1 = 0;
            in2 = 0;
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, imgWidth, imgHeight);

        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        textureOutputKnob.SetPosition(DefaultSize.x - 20);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void SetOffsetAndKnobParams()
    {
        // Stop NaN/inf propagation
        Predicate<float> invalidFloat = (v) => { return float.IsNaN(v) || float.IsInfinity(v); };
        if (invalidFloat(in1) || invalidFloat(in2))
        {
            in1 = 0;
            in2 = 0;
            return;
        }
        // Set the offset & knob params based on mode
        switch (offsetMode.Selected)
        {
            case "X/Y position":
                in1Knob.name = "X offset";
                in2Knob.name = "Y offset";
                in1min = -outputSize.x;
                in1max = outputSize.x;
                in2min = -outputSize.y;
                in2max = outputSize.y;
                offset.x = in1;
                offset.y = in2;
                break;
            case "X/Y speed":
                in1Knob.name = "X speed";
                in2Knob.name = "Y speed";
                // Arbitrarily choose (image size) pixels/sec as max scroll speed
                in1min = -outputSize.x;
                in1max = outputSize.x;
                in2min = -outputSize.y;
                in2max = outputSize.y;
                offset.x += Time.deltaTime*in1;
                offset.y += Time.deltaTime*in2;
                break;
            case "Speed/angle":
                // This is wasting a sqrt per frame, potential micro-optimization
                var absSpeed = Mathf.Sqrt(outputSize.x * outputSize.x + outputSize.y * outputSize.y);
                in1Knob.name = "Speed";
                in2Knob.name = "Angle";
                in1min = -absSpeed;
                in1max = absSpeed;
                in2min = 0;
                in2max = 360;
                var r = in1 * Time.deltaTime;
                offset += new Vector2(r * Mathf.Cos(in2 * Mathf.Deg2Rad), r * Mathf.Sin(in2 * Mathf.Deg2Rad));
                break;
        }
    }

    Vector2 mirrorSafeBounds = new Vector2(0,0);
    private void BoundOffset()
    {
        // Keep offset bounded by (2x) dimensions so that floating point coverage doesn't decrease
        // over long pans. use 2x so that mirrored textures don't jump on resetting the offset
        mirrorSafeBounds.x = 2 * (outputSize.x - 1);
        mirrorSafeBounds.y = 2 * (outputSize.y - 1);
        if (offset.x > mirrorSafeBounds.x)
        {
            offset.x -= mirrorSafeBounds.x;
        }
        else if (offset.x < -mirrorSafeBounds.x)
        {
            offset.x += mirrorSafeBounds.x;
        }
        if (offset.y > mirrorSafeBounds.y)
        {
            offset.y -= mirrorSafeBounds.y;
        }
        else if (offset.y < -mirrorSafeBounds.y)
        {
            offset.y += mirrorSafeBounds.y;
        }
    }

    private int ChooseKernel()
    {
        // Choose appropriate kernel ID based on pan options
        if (smoothTransitions)
        {
            switch (sampleMode.Selected)
            {
                case "Mirror":
                    return bilinearMirrorKernel;
                case "Repeat":
                    return bilinearRepeatKernel;
                case "Clamp":
                    return bilinearClampKernel;
            }
        } else
        {
            switch (sampleMode.Selected)
            {
                case "Mirror":
                    return pointMirrorKernel;
                case "Repeat":
                    return pointRepeatKernel;
                case "Clamp":
                    return pointClampKernel;
            }
        }
        return bilinearMirrorKernel;
    }

    float lastStep = 0;
    Vector2Int inputSize = new Vector2Int(0, 0);
    public override bool DoCalc()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        { // Reset outputs if no texture is available
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            if (outputTex != null)
                outputTex.Release();
            return true;
        }

        // Guard against multiple Calculate()'s per frame
        if (Time.time - lastStep > Time.deltaTime)
        {
            lastStep = Time.time;
        }
        else
        {
            textureOutputKnob.SetValue(outputTex);
            return true;
        }

        inputSize.x = tex.width;
        inputSize.y = tex.height;
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        in1 = in1Knob.connected() ? in1Knob.GetValue<float>() : in1;
        in2 = in2Knob.connected() ? in2Knob.GetValue<float>() : in2;

        SetOffsetAndKnobParams();
        BoundOffset();

        int panKernel = ChooseKernel();
        panShader.SetInt("width", tex.width);
        panShader.SetInt("height", tex.height);
        panShader.SetFloats("offset", offset.x, offset.y);
        panShader.SetTexture(panKernel, "OutputTex", outputTex);
        panShader.SetTexture(panKernel, "InputTex", tex);
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);
        panShader.Dispatch(panKernel, threadGroupX, threadGroupY, 1);

        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
