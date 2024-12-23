﻿using UnityEngine;
using UnityEditor;
using NodeEditorFramework;
using SecretFire.TextureSynth;

[Node(false, "Pattern/ContinuousWaves")]
public class ContinuousWaveNode : TickingNode
{
    public const string ID = "ContinuousWaveNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "ContinuousWaves"; } }
    private Vector2 _DefaultSize = new Vector2(250, 160);

    public override Vector2 DefaultSize => _DefaultSize;

    //[ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    //public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    private Vector2Int outputSize = new Vector2Int(75, 96);
    private RenderTexture outputTex;

    private ComputeShader continuousWaveShader;
    private int kernel;
    public override void DoInit()
    {
        continuousWaveShader = Resources.Load<ComputeShader>("NodeShaders/ContinuousWavePattern");
        kernel = continuousWaveShader.FindKernel("Pattern");
        InitializeRenderTexture();
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

    public override void NodeGUI()
    {
        
    }
}