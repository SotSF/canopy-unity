﻿using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Text;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

[Node(false, "Pattern/FluidSim")]
public class FluidSimNode : TickingNode
{
    public override string GetID => "FluidSimNode";
    public override string Title { get { return "FluidSim"; } }
    private Vector2 _DefaultSize = new Vector2(680, 240);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("velocityIn", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob velocityInputKnob;

    [ValueConnectionKnob("dyeIn", Direction.In, typeof(Texture), NodeSide.Top, 60)]
    public ValueConnectionKnob dyeInputKnob;

    [ValueConnectionKnob("dyeLevel", Direction.In, typeof(float))]
    public ValueConnectionKnob dyeInputLevelKnob;
    public float dyeInputLevel = 1;

    [ValueConnectionKnob("dyeDecay", Direction.In, typeof(float))]
    public ValueConnectionKnob dyeDecayKnob;
    public float dyeDecay = 0;

    [ValueConnectionKnob("forceMultiplier", Direction.In, typeof(float))]
    public ValueConnectionKnob forceMultiplierKnob;
    public float forceMultiplier = 1;

    [ValueConnectionKnob("timeMultiplier", Direction.In, typeof(float))]
    public ValueConnectionKnob timeMultiplierKnob;
    public float timeMultiplier = 1;

    [ValueConnectionKnob("Run", Direction.In, typeof(bool))]
    public ValueConnectionKnob runKnob;

    [ValueConnectionKnob("Reset", Direction.In, typeof(bool))]
    public ValueConnectionKnob resetKnob;

    [ValueConnectionKnob("ApplyForce", Direction.In, typeof(bool))]
    public ValueConnectionKnob applyForceKnob;

    [ValueConnectionKnob("ApplyDye", Direction.In, typeof(bool))]
    public ValueConnectionKnob applyDyeKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    private RenderTexture outputTex;
    public float timestep;

    public bool useBoundaries = true;
    public bool continuousVelocity = false;
    public bool continuousDye = false;
    public bool running = false;

    private ComputeShader fluidSimShader;
    private int advectionKernel;
    private int jacobiKernel;
    private int divergenceKernel;
    private int gradientDiffKernel;
    private int clearPressureKernel;
    private int forceKernel;
    private int dyeKernel;
    private int decayKernel;
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


    public override void DoInit()
    {
        fluidSimShader = Resources.Load<ComputeShader>("NodeShaders/EulerFluidSimPattern");
        dyeKernel = fluidSimShader.FindKernel("applyDye");
        decayKernel = fluidSimShader.FindKernel("decayDye");
        forceKernel = fluidSimShader.FindKernel("applyForce");
        jacobiKernel = fluidSimShader.FindKernel("jacobi"); ;
        advectionKernel = fluidSimShader.FindKernel("advect");
        divergenceKernel = fluidSimShader.FindKernel("divergence");
        gradientDiffKernel = fluidSimShader.FindKernel("gradientDiff");
        clearPressureKernel = fluidSimShader.FindKernel("clearPressure");
        verticalBoundaryKernel = fluidSimShader.FindKernel("verticalBoundary");
        horizontalBoundaryKernel = fluidSimShader.FindKernel("horizontalBoundary");


        dataBuffer = new ComputeBuffer(512, Constants.FLOAT_BYTES * Constants.VEC4_LENGTH);
        fluidSimShader.SetBuffer(horizontalBoundaryKernel, "dataBuffer", dataBuffer);
        fluidSimShader.SetInt("width", outputSize.x);
        fluidSimShader.SetInt("height", outputSize.y);
        bufferedData = new Vector4[512];
        InitializeRenderTextures();
        ClearRenderTextures();
    }
    
    bool clicked = false;
    //float viscosity = 1;
    GUIContent boundaryLabel = new GUIContent("Bounded", "Bound at the borders");
    GUIContent continuousVelocityLabel = new GUIContent("Continuous Velocity", "Add velocity texture every frame");
    GUIContent continuousDyeLabel = new GUIContent("Continuous dye", "Add dye every frame");
    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        velocityInputKnob.SetPosition(140);

        // Top row simulation control buttons
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref timeMultiplier, -1, 2, timeMultiplierKnob);
        FloatKnobOrSlider(ref dyeInputLevel, 0, 1, dyeInputLevelKnob);
        FloatKnobOrSlider(ref dyeDecay, 0, 1, dyeDecayKnob);
        FloatKnobOrSlider(ref forceMultiplier, 0, 4, forceMultiplierKnob);
        string cmd = running ? "Stop" : "Run";
        clicked = EventKnobOrButton(cmd, runKnob);
        if (clicked)
        {
            running = !running;
        }
        if (EventKnobOrButton("Apply dye", applyDyeKnob)) 
        { 
            AddDye();
        }
        if (EventKnobOrButton("Apply velocity", applyForceKnob))
        {
            ApplyVelocity();
        }
        if (EventKnobOrButton("Reset", resetKnob))
        {
            ClearRenderTextures();
            running = false;
        }
        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        // parameters / buttons
        GUILayout.BeginHorizontal();

        useBoundaries = RTEditorGUI.Toggle(useBoundaries, boundaryLabel);
        continuousVelocity = RTEditorGUI.Toggle(continuousVelocity, continuousVelocityLabel);
        continuousDye = RTEditorGUI.Toggle(continuousDye, continuousDyeLabel);
        //viscosity = RTEditorGUI.Slider(viscosity, 0.00001f, 100f);
        GUILayout.EndHorizontal();
        // Texture output
        GUILayout.BeginHorizontal();

        GUILayout.Box(dyeField, GUILayout.MaxWidth(128), GUILayout.MaxHeight(200));
        GUILayout.Box(velocityField, GUILayout.MaxWidth(128), GUILayout.MaxHeight(200));
        GUILayout.Box(pressureField, GUILayout.MaxWidth(128), GUILayout.MaxHeight(200));
        GUILayout.EndHorizontal();
        textureOutputKnob.SetPosition(DefaultSize.x - 40);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void AddDye()
    {
        fluidSimShader.SetFloat("dyeMultiplier", dyeInputLevel);
        Graphics.Blit(dyeInputKnob.GetValue<Texture>(), scaledBuffer);
        fluidSimShader.SetTexture(dyeKernel, "uField", dyeField);
        fluidSimShader.SetTexture(dyeKernel, "vField", scaledBuffer);
        ExecuteFullTexShader(dyeKernel);
        Graphics.Blit(resultField, dyeField);
    }

    private void ApplyVelocity(float multiplier = 1)
    {
        Texture input = velocityInputKnob.GetValue<Texture>();
        if (input != null && input.width > 0)
        {
            Graphics.Blit(input, scaledBuffer);
            fluidSimShader.SetFloat("forceMultiplier", multiplier);
            fluidSimShader.SetTexture(forceKernel, "uField", velocityField);
            fluidSimShader.SetTexture(forceKernel, "vField", scaledBuffer);
            ExecuteInteriorShader(forceKernel);
            Graphics.Blit(resultField, velocityField);
            ExecuteBoundaryShader(velocityField, -1);
        }
    }

    private void ExecuteInteriorShader(int kernel)
    {
        var groupSizeX = Mathf.CeilToInt((outputSize.x - 2) / 127.0f);
        var groupSizeY = Mathf.CeilToInt((outputSize.y - 2) / 2.0f);
        fluidSimShader.SetTexture(kernel, "Result", resultField);
        fluidSimShader.Dispatch(kernel, groupSizeX, groupSizeY, 1);
    }

    private void ExecuteFullTexShader(int kernel)
    {
        var groupSizeX = Mathf.CeilToInt((outputSize.x) / 16f);
        var groupSizeY = Mathf.CeilToInt((outputSize.y) / 16f);
        fluidSimShader.SetTexture(kernel, "Result", resultField);
        fluidSimShader.Dispatch(kernel, groupSizeX, groupSizeY, 1);
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
        fluidSimShader.SetFloat("dyeDecay", dyeDecay);
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
            ApplyVelocity(Time.deltaTime * forceMultiplier);
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

        // Decay dye
        fluidSimShader.SetTexture(decayKernel, "uField", dyeField);
        ExecuteFullTexShader(decayKernel);
        Graphics.Blit(resultField, dyeField);


        //Texture2D dbg = velocityField.ToTexture2D();
        //var colors = dbg.GetPixels();

        //this.TimedDebug(builder.ToString(), 3);
    }

    
    float lastStep = 0;
    public override bool DoCalc()
    {
        if (timeMultiplierKnob.connected())
        {
            timeMultiplier = timeMultiplierKnob.GetValue<float>();
        }
        if (applyForceKnob.GetValue<bool>())
        {
            ApplyVelocity(forceMultiplier);
        }
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
