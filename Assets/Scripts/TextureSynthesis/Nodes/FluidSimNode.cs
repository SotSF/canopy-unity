using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Text;
using NodeEditorFramework.Utilities;

[Node(false, "Inputs/FluidSim")]
public class FluidSimNode : TickingNode
{
    public override string GetID => "FluidSimNode";
    public override string Title { get { return "FluidSim"; } }
    public override Vector2 DefaultSize => new Vector2(620, 300);

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob velocityInputKnob;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 60)]
    public ValueConnectionKnob dyeInputKnob;

    [ValueConnectionKnob("dyeLevel", Direction.In, "Float")]
    public ValueConnectionKnob dyeInputLevel;

    [ValueConnectionKnob("timeMultiplier", Direction.In, "Float")]
    public ValueConnectionKnob timeMultiplierKnob;
    public float timeMultiplier = 1;

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
    //private int boundaryKernel;
    private int forceKernel;
    private int dyeKernel;
    private int horizontalBoundaryKernel;
    private int verticalBoundaryKernel;

    // Uses two channels (R,G) to store the 2vector field U(x,y)
    private RenderTexture velocityField;
    private RenderTexture pressureField;
    private RenderTexture dyeField;
    private RenderTexture resultField;
    private RenderTexture divergenceField;
    private RenderTexture scaledBuffer;

    private ComputeBuffer dataBuffer;
    private Vector4[] bufferedData;

    private Vector2Int outputSize = new Vector2Int(256, 256);
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

    private void OnDestroy()
    {
        RenderTexture[] textures = { outputTex, velocityField, pressureField, dyeField, resultField, divergenceField, scaledBuffer };
        foreach (var t in textures)
        {
            if (t != null)
                t.Release();
        }
        if (dataBuffer != null)
            dataBuffer.Release();
    }

    private void OnDisable()
    {
        OnDestroy();
    }


    private void Awake()
    {
        fluidSimShader = Resources.Load<ComputeShader>("NodeShaders/EulerFluidSimPattern");
        dyeKernel = fluidSimShader.FindKernel("applyDye");
        forceKernel = fluidSimShader.FindKernel("applyForce");
        jacobiKernel = fluidSimShader.FindKernel("jacobi"); ;
        advectionKernel = fluidSimShader.FindKernel("advect");
        divergenceKernel = fluidSimShader.FindKernel("divergence"); ;
        gradientDiffKernel = fluidSimShader.FindKernel("gradientDiff"); ;
        clearPressureKernel = fluidSimShader.FindKernel("clearPressure");
        verticalBoundaryKernel = fluidSimShader.FindKernel("verticalBoundary"); ;
        horizontalBoundaryKernel = fluidSimShader.FindKernel("horizontalBoundary"); ;


        dataBuffer = new ComputeBuffer(512, Constants.FLOAT_BYTES * Constants.VEC4_LENGTH);
        fluidSimShader.SetBuffer(horizontalBoundaryKernel, "dataBuffer", dataBuffer);
        fluidSimShader.SetInt("width", outputSize.x);
        fluidSimShader.SetInt("height", outputSize.y);
        bufferedData = new Vector4[512];
        InitializeRenderTextures();
        ClearRenderTextures();
    }

    bool useBoundaries = true;
    bool continuousVelocity = false;
    bool continuousDye = false;
    
    bool clicked = false;
    //float viscosity = 1;
    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        velocityInputKnob.SetPosition(140);
        dyeInputKnob.SetPosition(40);

        // Top row simulation control buttons
        GUILayout.BeginHorizontal();
        string cmd = running ? "Stop" : "Run";
        if (GUILayout.Button(cmd))
        {
            running = !running;
            clicked = true;
        }
        if (GUILayout.Button("Apply dye"))
        {
            AddDye();
        }
        if (GUILayout.Button("Apply velocity"))
        {
            ApplyVelocity();
        }
        if (GUILayout.Button("Reset"))
        {
            ClearRenderTextures();
            running = false;
        }
        GUILayout.EndHorizontal();

        // parameters / buttons
        GUILayout.BeginHorizontal();
        useBoundaries = RTEditorGUI.Toggle(useBoundaries, new GUIContent("Bounded", "Bound at the borders"));
        continuousVelocity = RTEditorGUI.Toggle(continuousVelocity, new GUIContent("Continuous Velocity", "Add velocity texture every frame"));
        continuousDye = RTEditorGUI.Toggle(continuousDye, new GUIContent("Continuous dye", "Add dye every frame"));
        //viscosity = RTEditorGUI.Slider(viscosity, 0.00001f, 100f);
        GUILayout.EndHorizontal();
        // Texture output
        GUILayout.BeginHorizontal();
        timeMultiplierKnob.DisplayLayout();
        if (!timeMultiplierKnob.connected())
        {
            timeMultiplier = RTEditorGUI.Slider(timeMultiplier, -1, 1);
        } else
        {
            timeMultiplier = timeMultiplierKnob.GetValue<float>();
        }

        GUILayout.Box(dyeField, GUILayout.MaxWidth(200), GUILayout.MaxHeight(200));
        GUILayout.Box(velocityField, GUILayout.MaxWidth(200), GUILayout.MaxHeight(200));
        GUILayout.Box(pressureField, GUILayout.MaxWidth(200), GUILayout.MaxHeight(200));
        GUILayout.EndHorizontal();
        textureOutputKnob.SetPosition(DefaultSize.x - 40);
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void AddDye()
    {
        fluidSimShader.SetFloat("dyeMultiplier", dyeInputLevel.GetValue<float>());
        Graphics.Blit(dyeInputKnob.GetValue<Texture>(), scaledBuffer);
        fluidSimShader.SetTexture(dyeKernel, "uField", dyeField);
        fluidSimShader.SetTexture(dyeKernel, "vField", scaledBuffer);
        ExecuteFullTexShader(dyeKernel);
        Graphics.Blit(resultField, dyeField);
    }

    private void ApplyVelocity()
    {
        Texture input = velocityInputKnob.GetValue<Texture>();
        if (input != null && input.width > 0)
        {
            Graphics.Blit(input, scaledBuffer);
            fluidSimShader.SetTexture(forceKernel, "uField", velocityField);
            fluidSimShader.SetTexture(forceKernel, "vField", scaledBuffer);
            ExecuteInteriorShader(forceKernel);
            Graphics.Blit(resultField, velocityField);
            ExecuteBoundaryShader(velocityField, -1);
        }
    }

    private void ExecuteInteriorShader(int kernel)
    {
        Vector2Int groupSize = new Vector2Int(Mathf.CeilToInt((outputSize.x-2) / 127.0f), Mathf.CeilToInt((outputSize.y-2) / 2.0f));
        fluidSimShader.SetTexture(kernel, "Result", resultField);
        fluidSimShader.Dispatch(kernel, groupSize.x, groupSize.y, 1);
    }

    private void ExecuteFullTexShader(int kernel)
    {
        Vector2Int groupSize = new Vector2Int(Mathf.CeilToInt((outputSize.x) / 16f), Mathf.CeilToInt((outputSize.y) / 16f));
        fluidSimShader.SetTexture(kernel, "Result", resultField);
        fluidSimShader.Dispatch(kernel, groupSize.x, groupSize.y, 1);
    }

    private void ExecuteBoundaryShader(RenderTexture field, float scale)
    {
        if (useBoundaries)
        {
            fluidSimShader.SetFloat("boundaryScale", scale);
            fluidSimShader.SetTexture(horizontalBoundaryKernel, "Result", field);
            fluidSimShader.Dispatch(horizontalBoundaryKernel, 1, 2, 1);
            fluidSimShader.SetTexture(verticalBoundaryKernel, "Result", field);
            fluidSimShader.Dispatch(verticalBoundaryKernel, 2, 1, 1);
        }
    }

    private void SimulateFluid()
    {
        float dx2 = outputSize.x * outputSize.x;
        fluidSimShader.SetInt("width", outputSize.x);
        fluidSimShader.SetInt("height", outputSize.y);
        //fluidSimShader.SetFloat("dissipation", 0.0f);
        fluidSimShader.SetFloat("timestep", timeMultiplier * timestep);

        //Apply velocity boundary
        ExecuteBoundaryShader(velocityField, -1);

        // Advect velocity
        fluidSimShader.SetFloat("gridNormalizingFactor", 1.0f / (outputSize.x));
        fluidSimShader.SetTexture(advectionKernel, "uField", velocityField);
        fluidSimShader.SetTexture(advectionKernel, "vField", velocityField);
        ExecuteFullTexShader(advectionKernel);
        Graphics.Blit(resultField, velocityField);

        if (continuousVelocity)
        {
            ApplyVelocity();
        }

        // Compute diffusion
        //fluidSimShader.SetFloat("jacobiAlpha", (dx2) / (viscosity * timestep));
        //fluidSimShader.SetFloat("jacobiRBeta", 1.0f / (4 + (dx2) / (viscosity * timestep)));
        //for (int i = 0; i < 60; i++)
        //{
        //    fluidSimShader.SetTexture(jacobiKernel, "vField", velocityField);
        //    fluidSimShader.SetTexture(jacobiKernel, "uField", velocityField);
        //    ExecuteInteriorShader(jacobiKernel);
        //    Graphics.Blit(resultField, velocityField);
        //}

        // Compute divergence
        fluidSimShader.SetTexture(divergenceKernel, "uField", velocityField);
        ExecuteInteriorShader(divergenceKernel);
        Graphics.Blit(resultField, divergenceField);

        //Clear pressure field for jacobi iteration
        fluidSimShader.SetTexture(clearPressureKernel, "uField", pressureField);
        ExecuteFullTexShader(clearPressureKernel);
        Graphics.Blit(resultField, pressureField);

        //Apply pressure boundary scale
        fluidSimShader.SetFloat("boundaryScale", 1);

        // Compute new pressure field via jacobi
        fluidSimShader.SetFloat("jacobiAlpha", -1 * (dx2));
        fluidSimShader.SetFloat("jacobiRBeta", 0.25f);
        fluidSimShader.SetTexture(jacobiKernel, "vField", divergenceField);
        fluidSimShader.SetTexture(jacobiKernel, "uField", pressureField);
        for (int i = 0; i < 60; i++)
        {
            //Apply pressure boundaries per step
            ExecuteBoundaryShader(pressureField, 1);

            ExecuteInteriorShader(jacobiKernel);
            Graphics.Blit(resultField, pressureField);
        }

        //Reapply velocity boundary after
        ExecuteBoundaryShader(velocityField, -1);

        // Subtract pressure gradient from intermediate velocity field
        fluidSimShader.SetTexture(gradientDiffKernel, "uField", velocityField);
        fluidSimShader.SetTexture(gradientDiffKernel, "vField", pressureField);
        ExecuteInteriorShader(gradientDiffKernel);
        Graphics.Blit(resultField, velocityField);

        //dataBuffer.GetData(bufferedData);
        //StringBuilder builder = new StringBuilder();
        //for (int i = 0; i < 512; i++)
        //{
        //    builder.Append(string.Format("[{0:0.000}, {1:0.000}], ", bufferedData[i].x, bufferedData[i].y));
        //}
        //this.TimedDebug(builder.ToString(), 2);

        //Apply dye boundary
        ExecuteBoundaryShader(dyeField, 0);

        // Advect dye
        fluidSimShader.SetTexture(advectionKernel, "uField", velocityField);
        fluidSimShader.SetTexture(advectionKernel, "vField", dyeField);
        ExecuteFullTexShader(advectionKernel);
        Graphics.Blit(resultField, dyeField);

        //Texture2D dbg = velocityField.ToTexture2D();
        //var colors = dbg.GetPixels();

        //this.TimedDebug(builder.ToString(), 3);
    }

    float timestep;
    float lastStep = 0;
    public override bool Calculate()
    {
        if (running && Time.time - lastStep > 1/60f)
        {
            if (continuousDye)
            {
                AddDye();
            }
            if (clicked)
            {
                timestep = Time.deltaTime;
            } else
            {
                timestep = Time.time - lastStep;
            }
            lastStep = Time.time;
            SimulateFluid();
        }
        if (clicked)
        {
            clicked = false;
        }
        textureOutputKnob.SetValue<Texture>(dyeField);
        return true;
    }
}
