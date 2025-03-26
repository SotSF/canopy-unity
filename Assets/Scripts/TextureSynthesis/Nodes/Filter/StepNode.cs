using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Filter/Step")]
public class StepNode : TextureSynthNode
{
    public const string ID = "StepNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Step"; } }
    private Vector2 _DefaultSize = new Vector2(150, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Min", Direction.In, typeof(float))]
    public ValueConnectionKnob minInputKnob;
    public float min = 0;

    [ValueConnectionKnob("Max", Direction.In, typeof(float))]
    public ValueConnectionKnob maxInputKnob;
    public float max = 1;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture),NodeSide.Bottom, 180)]
    public ValueConnectionKnob textureOutputKnob;

    public bool smooth = false;

    private ComputeShader stepShader;
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;

    private int stepKernel;

    public override void DoInit()
    {
        stepShader = Resources.Load<ComputeShader>("NodeShaders/StepFilter");
        stepKernel = stepShader.FindKernel("StepKernel");
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
        textureInputKnob.SetPosition(20);

        FloatKnobOrSlider(ref min, 0, 1, minInputKnob);
        FloatKnobOrSlider(ref max, 0, 1, maxInputKnob);

        GUILayout.BeginHorizontal();
       
        GUILayout.BeginVertical();
        smooth = GUILayout.Toggle(smooth, "Smooth", GUILayout.MaxWidth(64));
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxHeight(64), GUILayout.MaxWidth(64));
        GUILayout.Space(4);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        textureOutputKnob.SetPosition(DefaultSize.x - 20);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        min = minInputKnob.connected() ? minInputKnob.GetValue<float>() : min;
        max = maxInputKnob.connected() ? maxInputKnob.GetValue<float>() : max;
        Texture inputTex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || inputTex == null)
        { // Reset outputs if no texture is available
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            return true;
        }
        if (outputSize.x != inputTex.width || outputSize.y != inputTex.height)
        {
            outputSize = new Vector2Int(inputTex.width, inputTex.height);
            InitializeRenderTexture();
        }
        stepShader.SetBool("smooth", smooth);
        stepShader.SetTexture(stepKernel, "InputTex", inputTex);
        stepShader.SetTexture(stepKernel, "OutputTex", outputTex);
        stepShader.SetInt("iWidth", inputTex.width);
        stepShader.SetInt("iHeight", inputTex.height);
        stepShader.SetInt("oWidth", outputTex.width);
        stepShader.SetInt("oHeight", outputTex.height);
        stepShader.SetFloat("min", min);
        stepShader.SetFloat("max", max);
        var threadGroupX = Mathf.CeilToInt(outputTex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(outputTex.height / 16.0f);
        stepShader.Dispatch(stepKernel, threadGroupX, threadGroupY, 1);
        // Assign output channels
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}
