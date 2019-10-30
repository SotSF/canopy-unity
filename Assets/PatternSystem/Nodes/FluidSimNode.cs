using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Text;

[Node(false, "Inputs/FluidSim")]
public class FluidSimNode : TickingNode
{
    public override string GetID => "FluidSimNode";
    public override string Title { get { return "FluidSim"; } }
    public override Vector2 DefaultSize => new Vector2(620, 250);

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob velocityInputKnob;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 60)]
    public ValueConnectionKnob dyeInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    public RenderTexture outputTex;

    private bool running = false;
    private ComputeShader fluidSimShader;
    private int advectionKernel;
    private int jacobiKernel;
    private int divergenceKernel;
    private int gradientDiffKernel;
    private int clearPressureKernel;
    private int boundaryKernel;
    private int forceKernel;

    // Uses two channels (R,G) to store the 2vector field U(x,y)
    private RenderTexture velocityField;
    // Store up to 4 channels of scalar fields, eg pressure, temperature, density
    private RenderTexture pressureField;
    // Store a dye concentration field
    private RenderTexture dyeField;
    // Result field for outputs of shader computations
    private RenderTexture resultField;
    // Divergence field for stores divergence during pressure iteration
    private RenderTexture divergenceField;
    private RenderTexture scaledBuffer;

    private Vector2Int outputSize = new Vector2Int(256,256);
    private RenderTextureFormat rtFmt = RenderTextureFormat.ARGBFloat;

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
        velocityField = MakeRenderTexture();
        pressureField = MakeRenderTexture();
        dyeField = MakeRenderTexture();
        resultField = MakeRenderTexture();
        divergenceField = MakeRenderTexture();
        scaledBuffer = MakeRenderTexture();
    }

    private void OnDestroy()
    {
        RenderTexture[] textures = { outputTex, velocityField, pressureField, dyeField, resultField, divergenceField, scaledBuffer };
        foreach (var t in textures)
        {
            if (t != null)
                t.Release();
        }
    }


    private void Awake()
    {
        fluidSimShader = Resources.Load<ComputeShader>("Patterns/PatternShaders/EulerFluidSimPattern");
        advectionKernel = fluidSimShader.FindKernel("advect");
        jacobiKernel = fluidSimShader.FindKernel("jacobi"); ;
        divergenceKernel = fluidSimShader.FindKernel("divergence"); ;
        gradientDiffKernel = fluidSimShader.FindKernel("gradientDiff"); ;
        clearPressureKernel = fluidSimShader.FindKernel("clearPressure");
        boundaryKernel = fluidSimShader.FindKernel("boundary");
        forceKernel = fluidSimShader.FindKernel("applyForce");
        InitializeRenderTextures();
        ClearRenderTextures();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        velocityInputKnob.SetPosition(140);
        dyeInputKnob.SetPosition(40);
        GUILayout.BeginHorizontal();
        
        string cmd = running ? "Stop" : "Run";
        if (GUILayout.Button(cmd))
        {
            running = !running;
        }
        if (GUILayout.Button("Apply dye"))
        {
            Graphics.Blit(dyeInputKnob.GetValue<Texture>(), dyeField);
        }
        if (GUILayout.Button("Apply velocity"))
        {
            Graphics.Blit(velocityInputKnob.GetValue<Texture>(), scaledBuffer);
            fluidSimShader.SetTexture(forceKernel, "uField", velocityField);
            fluidSimShader.SetTexture(forceKernel, "vField", scaledBuffer);
            ExecuteShader(forceKernel);
            Graphics.Blit(resultField, velocityField);
        }
        if (GUILayout.Button("Reset"))
        {
            ClearRenderTextures();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Box(dyeField, GUILayout.MaxWidth(200), GUILayout.MaxHeight(200));
        GUILayout.Box(velocityField, GUILayout.MaxWidth(200), GUILayout.MaxHeight(200));
        GUILayout.Box(pressureField, GUILayout.MaxWidth(200), GUILayout.MaxHeight(200));
        GUILayout.EndHorizontal();
        textureOutputKnob.SetPosition(DefaultSize.x - 40);
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void ClearRenderTextures()
    {
        RenderTexture.active = velocityField;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = pressureField;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = dyeField;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;
    }

    private void ExecuteShader(int kernel)
    {
        Vector2Int groupSize = new Vector2Int(Mathf.CeilToInt(outputSize.x / 16.0f), Mathf.CeilToInt(outputSize.y / 16.0f));
        fluidSimShader.SetTexture(kernel, "Result", resultField);
        fluidSimShader.Dispatch(kernel, groupSize.x, groupSize.y, 1);
    }

    private void SimulateFluid()
    {
        Vector2Int groupSize = new Vector2Int(Mathf.CeilToInt(outputSize.x / 16.0f), Mathf.CeilToInt(outputSize.y / 16.0f));
        float dx2 = outputSize.x * outputSize.x;
        fluidSimShader.SetFloat("width", outputSize.x);
        fluidSimShader.SetFloat("height", outputSize.y);
        fluidSimShader.SetFloat("dissipation", 0.0f);
        // Advect velocity
        fluidSimShader.SetFloat("timestep", Time.deltaTime);
        fluidSimShader.SetFloat("gridNormalizingFactor", 1.0f / outputSize.x);
        fluidSimShader.SetTexture(advectionKernel, "uField", velocityField);
        fluidSimShader.SetTexture(advectionKernel, "vField", velocityField);
        ExecuteShader(advectionKernel);
        Graphics.Blit(resultField, velocityField);

        // Compute diffusion
        //var n = 0.000001f;
        //fluidSimShader.SetFloat("jacobiAlpha", (dx2) / (n*Time.deltaTime));
        //fluidSimShader.SetFloat("jacobiRBeta", 1.0f / (4+(dx2) / (n*Time.deltaTime)));
        //for (int i = 0; i < 20; i++)
        //{
        //    fluidSimShader.SetTexture(jacobiKernel, "vField", velocityField);
        //    fluidSimShader.SetTexture(jacobiKernel, "uField", velocityField);
        //    ExecuteShader(jacobiKernel);
        //    Graphics.Blit(resultField, velocityField);
        //}

        // Compute divergence
        fluidSimShader.SetTexture(divergenceKernel, "uField", velocityField);
        ExecuteShader(divergenceKernel);
        Graphics.Blit(resultField, divergenceField);

        //Clear pressure field for jacobi iteration
        fluidSimShader.SetTexture(clearPressureKernel, "uField", pressureField);
        ExecuteShader(clearPressureKernel);
        Graphics.Blit(resultField, pressureField);

        // Compute new pressure field via jacobi
        fluidSimShader.SetFloat("jacobiAlpha", -1 * (dx2));
        fluidSimShader.SetFloat("jacobiRBeta", 0.25f);
        fluidSimShader.SetTexture(jacobiKernel, "vField", divergenceField);
        for (int i = 0; i < 40; i++)
        {
            fluidSimShader.SetTexture(jacobiKernel, "uField", pressureField);
            ExecuteShader(jacobiKernel);
            Graphics.Blit(resultField, pressureField);
        }
        //Apply pressure boundary
        fluidSimShader.SetFloat("boundaryScale", 1);
        fluidSimShader.SetTexture(boundaryKernel, "uField", pressureField);
        ExecuteShader(boundaryKernel);
        Graphics.Blit(resultField, pressureField);

        // Subtract pressure gradient from intermediate velocity field
        fluidSimShader.SetTexture(gradientDiffKernel, "uField", velocityField);
        fluidSimShader.SetTexture(gradientDiffKernel, "vField", pressureField);
        ExecuteShader(gradientDiffKernel);
        Graphics.Blit(resultField, velocityField);

        //Apply velocity boundary
        fluidSimShader.SetFloat("boundaryScale", -1);
        fluidSimShader.SetTexture(boundaryKernel, "uField", velocityField);
        ExecuteShader(boundaryKernel);
        Graphics.Blit(resultField, velocityField);

        //Texture2D dbg = velocityField.ToTexture2D();
        //var colors = dbg.GetPixels();
        //StringBuilder builder = new StringBuilder();
        //for (int i = 0; i < 40; i++)
        //{
        //    builder.Append(string.Format("[{0:0.000}, {1:0.000}, {2:0.000}, {3:0.000}], ",colors[i].r, colors[i].g, colors[i].b, colors[i].a));
        //}
        //this.TimedDebug(builder.ToString(), 3);

        // Advect dye
        fluidSimShader.SetTexture(advectionKernel, "uField", velocityField);
        fluidSimShader.SetTexture(advectionKernel, "vField", dyeField);
        ExecuteShader(advectionKernel);
        Graphics.Blit(resultField, dyeField);
    }

    public override bool Calculate()
    {
        if (running)
        {
            SimulateFluid();
        }
        textureOutputKnob.SetValue<Texture>(dyeField);
        return true;
    }
}
