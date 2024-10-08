using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;
using System;
using SecretFire.TextureSynth;

[Node(false, "Canopy/CanopyProjection")]
public class CanopyNode : TextureSynthNode
{
    public const string ID = "canopyMain";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Canopy"; } }
    private Vector2 _DefaultSize = new Vector2(250, 160);

    public override Vector2 DefaultSize => _DefaultSize;


    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture outputTex;
    private ComputeShader canopyMainShader;
    //private ComputeBuffer dataBuffer;
    private Vector3[] colorData;
    private int mainKernel;
    public bool polarize;
    public bool seamless;
    private Light lightCaster;

    private bool hasAwoken = false;

    private void ReleaseTextures()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
    }

    private void OnDestroy()
    {
        ReleaseTextures();
    }

    private void OnDisable()
    {
        ReleaseTextures();
    }

    public override void DoInit()
    {
        canopyMainShader = Resources.Load<ComputeShader>("NodeShaders/CanopyMain");
        mainKernel = canopyMainShader.FindKernel("CanopyMain");
        //dataBuffer = new ComputeBuffer(Constants.NUM_LEDS, Constants.FLOAT_BYTES * Constants.VEC3_LENGTH);
        colorData = new Vector3[Constants.NUM_LEDS];
        InitializeTextures();
        //canopyMainShader.SetBuffer(mainKernel, "dataBuffer", dataBuffer);
        canopyMainShader.SetTexture(mainKernel, "OutputTex", outputTex);
        lightCaster = GameObject.Find("Canopy").GetComponentInChildren<Light>();
        RenderToCanopySimulation(outputTex);
        Debug.Log("Canopy called Initialize");
    }

    private void InitializeTextures()
{
        ReleaseTextures();
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
        if (GUILayout.Button("Reinitialize"))
        {
            initialized = false;
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
        canopyMaterial.SetTexture("Frame", texture);
    }

    public override bool DoCalc()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (tex != null)
        {
            // Process data one frame delayed
            //dataBuffer.GetData(colorData);
            SetLightColor();
            int kernelId = mainKernel;
            canopyMainShader.SetTexture(kernelId, "InputTex", tex);
            canopyMainShader.SetInt("height", tex.height);

            //Execute compute shader
            canopyMainShader.SetBool("polarize", polarize);
            canopyMainShader.SetInt("width", tex.width);

            canopyMainShader.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);
            textureOutputKnob.SetValue(outputTex);
            // Assign output channels
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
