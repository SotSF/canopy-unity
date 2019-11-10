using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;
using System;

[Node(false, "Canopy/Main")]
public class CanopyNode : Node
{
    public const string ID = "canopyMain";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Canopy"; } }
    public override Vector2 DefaultSize { get { return new Vector2(250, 160); } }

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;


    private Camera canopyCam;
    private RenderTexture camTex;
    private RenderTexture kaleidoscopeElementTexture;
    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture outputTex;
    private ComputeShader canopyMainShader;
    private ComputeBuffer dataBuffer;
    private Vector3[] colorData;
    private int kernelId;
    public bool polarize;
    public bool seamless;
    public bool fitX;
    public bool fitY;
    private Light lightCaster;

    private void Awake()
    {
        Debug.Log("CanopyMain awoke");
        if (Application.isPlaying)
        {
            canopyMainShader = Resources.Load<ComputeShader>("FilterShaders/CanopyMain");
            kernelId = canopyMainShader.FindKernel("CanopyMain");
            dataBuffer = new ComputeBuffer(Constants.NUM_LEDS, Constants.FLOAT_BYTES * Constants.VEC3_LENGTH);
            colorData = new Vector3[Constants.NUM_LEDS];
            InitializeTextures();
            canopyMainShader.SetBuffer(kernelId, "dataBuffer", dataBuffer);
            canopyMainShader.SetTexture(kernelId, "OutputTex", outputTex);
            lightCaster = GameObject.Find("Canopy").GetComponentInChildren<Light>();
            RenderToCanopySimulation(outputTex);
        }

    }

    private void OnDestroy()
    {
        if (dataBuffer != null)
            dataBuffer.Release();
    }

    private void InitializeTextures()
    {
        // Create a texture that is half of the canopy size
        kaleidoscopeElementTexture = new RenderTexture(75, 48, 24);
        kaleidoscopeElementTexture.enableRandomWrite = true;
        kaleidoscopeElementTexture.Create();

        //outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
        outputTex.enableRandomWrite = true;
        //outputTex.filterMode = FilterMode.Bilinear;
        //outputTex.wrapMode = TextureWrapMode.Repeat;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        float edgeOffset = 35;
        GUILayout.BeginVertical();
        //textureInputKnob.DisplayLayout();
        textureInputKnob.SetPosition(edgeOffset);

        GUILayout.BeginHorizontal();
        polarize = RTEditorGUI.Toggle(polarize, new GUIContent("Polarize", "Polarize the input to be in canopy-world space"));
        seamless = RTEditorGUI.Toggle(seamless, new GUIContent("Seamless", "Apply a kaleidoscope effect such that the output is seamless"));
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
        var lastBox = GUILayoutUtility.GetLastRect();
        Rect inOutTextureBox = new Rect(lastBox.x + 15, lastBox.yMax + 4, 220, 80);
        Rect textureArea = new Rect(inOutTextureBox);
        textureArea.y += 30;
        //GUILayout.BeginArea(InOutTextureBox);
        GUILayout.BeginArea(textureArea);
        GUILayout.BeginHorizontal();
        GUI.Box(new Rect(0,0, 220, 80), "Foo");
        GUILayout.Box(textureInputKnob.GetValue<Texture>(), GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.FlexibleSpace();
        //RTTextureViz.DrawTexture(textureInputKnob.GetValue<Texture>(), 64);

        //RTTextureViz.DrawTexture(outputTex, 64);
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        //textureOutputKnob.DisplayLayout();
        var rightSideOffset = DefaultSize.x - edgeOffset;
        textureOutputKnob.SetPosition(rightSideOffset);
        GUILayout.EndVertical();



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
            if (!polarize && seamless) {
                // If in seamless mode, copy and crop the input texture into the kaleidoscope
                // element texture and send to the compute shader
                Vector2 scale = new Vector2(1, 1);
                Vector2 offset = new Vector2(0,0);
                Graphics.Blit(tex, kaleidoscopeElementTexture, scale, offset);
                canopyMainShader.SetTexture(kernelId, "InputTex", kaleidoscopeElementTexture);
                canopyMainShader.SetInt("height", Math.Min(tex.height, Constants.NUM_STRIPS / 2 - 1));
            } else {
                // Otherwise, send the unchanged input texture to the compute shader
                canopyMainShader.SetTexture(kernelId, "InputTex", tex);
                canopyMainShader.SetInt("height", tex.height);
            }

            //Execute compute shader
            canopyMainShader.SetBool("polarize", polarize);
            canopyMainShader.SetBool("seamless", seamless);
            canopyMainShader.SetBool("fitX", fitX);
            canopyMainShader.SetBool("fitY", fitY);
            canopyMainShader.SetInt("width", tex.width);

            canopyMainShader.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);
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
