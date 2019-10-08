using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;


[Node(false, "Canopy/Main")]
public class CanopyNode : Node
{
    public const string ID = "canopyMain";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Canopy"; } }
    public override Vector2 DefaultSize { get { return new Vector2(400, 250); } }

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

    private void Awake()
    {
        Debug.Log("CanopyMain awoke");
        arrayFormatter = Resources.Load<ComputeShader>("FilterShaders/CanopyMain");
        kernelId = arrayFormatter.FindKernel("CSMain");
        dataBuffer = new ComputeBuffer(Constants.NUM_LEDS, Constants.FLOAT_BYTES * Constants.VEC3_LENGTH);
        colorData = new Vector3[Constants.NUM_LEDS];

        InitializeOutputTexture();
        arrayFormatter.SetBuffer(kernelId, "dataBuffer", dataBuffer);
        arrayFormatter.SetTexture(kernelId, "OutputTex", outputTex);
        if (Application.isPlaying)
        {
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

        polarize = RTEditorGUI.Toggle(polarize, new GUIContent("Polarize", "Polarize the input to be in canopy-world space"));
        //scale = RTEditorGUI.Toggle(scale, new GUIContent("Polarize", "Polarize the input to be in canopy-world space"));

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
        Material canopyMaterial = GameObject.Find("NodeUI").GetComponent<NodeUIController>().canopyMaterial;
        var textures = canopyMaterial.GetTexturePropertyNames();
        foreach (string tex in textures)
        {
            canopyMaterial.SetTexture(tex, texture);
        }
    }

    public override bool Calculate()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (tex != null) {
            //Execute compute shader
            arrayFormatter.SetBool("polarize", polarize);
            arrayFormatter.SetTexture(kernelId, "InputTex", tex);
            arrayFormatter.SetInt("width", tex.width);
            arrayFormatter.SetInt("height", tex.height);

            arrayFormatter.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);
            // Assign output channels
            textureOutputKnob.SetValue(outputTex);
        }   
        
        // Open questions:
        // Do polarization unwrap / scaling for canopy size within this
        // node? or expect it to be done already via node editor stuff


        return true;
    }
}