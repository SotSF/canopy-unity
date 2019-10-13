using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using System.Collections.Generic;
using UnityEngine;


[Node(false, "Canopy/Main")]
public class CanopyNode : Node
{
    public const string ID = "canopyMain";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Canopy"; } }
    public override Vector2 DefaultSize { get { return new Vector2(250, 250); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture))]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    public ValueConnectionKnob textureOutputKnob;


    private Camera canopyCam;
    private RenderTexture camTex;
    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture outputTex;
    private ComputeShader arrayFormatter;
    private ComputeBuffer dataBuffer;
    private Vector3[] colorData;
    private int kernelId;
    private bool polarize;
    private bool scale;
    private bool fitX;
    private bool fitY;
    private Light lightCaster;

    private void Awake()
    {
        Debug.Log("CanopyMain awoke");
        if (Application.isPlaying)
        {
            arrayFormatter = Resources.Load<ComputeShader>("FilterShaders/CanopyMain");
            kernelId = arrayFormatter.FindKernel("CSMain");
            dataBuffer = new ComputeBuffer(Constants.NUM_LEDS, Constants.FLOAT_BYTES * Constants.VEC3_LENGTH);
            colorData = new Vector3[Constants.NUM_LEDS];
            InitializeOutputTexture();
            arrayFormatter.SetBuffer(kernelId, "dataBuffer", dataBuffer);
            arrayFormatter.SetTexture(kernelId, "OutputTex", outputTex);
            lightCaster = GameObject.Find("Canopy").GetComponentInChildren<Light>();
            RenderToCanopySimulation(outputTex);
        }
    }

    private void OnDestroy()
    {
        if (dataBuffer != null)
            dataBuffer.Release();
    }

    private void InitializeOutputTexture()
    {
        //outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        textureInputKnob.DisplayLayout();

        GUILayout.BeginHorizontal();
        polarize = RTEditorGUI.Toggle(polarize, new GUIContent("Polarize", "Polarize the input to be in canopy-world space"));
        if (RTEditorGUI.Toggle(fitX, new GUIContent("Fit X", "Scale the input to fit the canopy in X"))){
            fitX = true;
            fitY = false;
        } else
        {
            fitX = false;
        }
        if (RTEditorGUI.Toggle(fitY, new GUIContent("Fit Y", "Scale the input to fit the canopy in Y"))) {
            fitX = false;
            fitY = true;
        } else
        {
            fitY = false;
        }
        GUILayout.EndHorizontal();

        RTTextureViz.DrawTexture(textureInputKnob.GetValue<Texture>(), 64);
        GUILayout.Label("input");

        RTTextureViz.DrawTexture(outputTex, 64);
        GUILayout.Label("output");

        GUILayout.EndVertical();

        textureOutputKnob.DisplayLayout();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public void RenderToCanopySimulation(Texture texture)
    {
        var canopyMaterial = NodeUIController.instance.canopyMaterial;
        canopyMaterial.SetTexture("_Frame", texture);
    }

    public override bool Calculate()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (tex != null)
        {
            //Execute compute shader
            arrayFormatter.SetBool("polarize", polarize);
            arrayFormatter.SetBool("fitX", fitX);
            arrayFormatter.SetBool("fitY", fitY);
            arrayFormatter.SetTexture(kernelId, "InputTex", tex);
            arrayFormatter.SetInt("width", tex.width);
            arrayFormatter.SetInt("height", tex.height);

            arrayFormatter.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);
            dataBuffer.GetData(colorData);
            SetLightColor();
            // Assign output channels
            textureOutputKnob.SetValue(outputTex);
        }
        return true;
    }

    private void SetLightColor()
    {
        Vector3 avg = Vector3.zero;
        int litPixelCount = 0;
        foreach (var pixel in colorData)
        {
            if (pixel.x + pixel.y + pixel.z > .5)
            {
                avg += pixel;
                litPixelCount++;
            }
        }
        avg /= litPixelCount;
        Color c = new Color(avg.x, avg.y, avg.z);
        if (lightCaster != null && (!float.IsNaN(c.r) && !float.IsNaN(c.g) && !float.IsNaN(c.b) && !float.IsNaN(c.a)))
        {
            lightCaster.color = Color.Lerp(lightCaster.color, c, 0.5f);
        }
    }
}