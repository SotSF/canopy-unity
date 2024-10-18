using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using NUnit.Framework;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


abstract public class DynamicPatternNode : TickingNode
{
    private Vector2 _DefaultSize = new Vector2(150, 100);

    public override Vector2 DefaultSize => _DefaultSize;

     [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;

    protected RenderTexture outputTex;

    protected Vector2Int outputSize = Vector2Int.zero;

    protected List<string> inputPortNames;
    protected List<Type> inputPortTypes;

    public override void DoInit()
    {

    }

    protected void SetupPorts()
    {
        bool resized = false;
        for (int i = dynamicConnectionPorts.Count; i < inputPortNames.Count; i++)
        {
            var portSide = (typeof(Texture)).IsAssignableFrom(inputPortTypes[i]) ? NodeSide.Top : NodeSide.Left; 
            ValueConnectionKnobAttribute inputKnobAttribs = new ValueConnectionKnobAttribute(inputPortNames[i], Direction.In, inputPortTypes[i], portSide);
            CreateValueConnectionKnob(inputKnobAttribs);
            resized = true;
        }
        if (resized)
        {
            SetSize();
        }
    }

    int signalPorts => dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).side == NodeSide.Left).Count();
    int texPorts => dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).side == NodeSide.Top).Count();
    protected void SetSize()
    {

        _DefaultSize = new Vector2(
            (1 + texPorts) * 100,
            (1 + signalPorts) * 60 + 50
        );
    }

    protected void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 16);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    GUILayoutOption width25 = GUILayout.Width(25);
    GUILayoutOption width50 = GUILayout.Width(50);
    GUILayoutOption width100 = GUILayout.Width(100);
    GUILayoutOption height25 = GUILayout.Height(25);
    GUILayoutOption height50 = GUILayout.Height(50);
    GUILayoutOption height100 = GUILayout.Height(100);
    public override void NodeGUI()
    {
        SetupPorts();
        //Draw tex ports across top
        GUILayout.BeginHorizontal();
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.side == NodeSide.Top)
            {
                GUILayout.BeginVertical();
                var portName = inputPortNames[i].Substring("_LW_".Length);
                GUILayout.Label(portName, width100);
                GUILayout.Space(4);
                GUILayout.Box(port.GetValue<Texture>(), width25, height25);
                port.SetPosition();
                GUILayout.EndVertical();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        // Draw signal ports up/down
        GUILayout.BeginVertical();
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.side == NodeSide.Left)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(2);
                var portName = inputPortNames[i].Substring("_LW_".Length);
                if (port.valueType == typeof(float))
                {
                    GUILayout.Label(string.Format($"{portName}: {port.GetValue<float>():0.00}"));
                }
                else
                {
                    GUILayout.Label(string.Format($"{portName}: {port.GetValue()}"));
                }
                port.SetPosition();
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndVertical();
        // End signal ports

        // Draw rendered image
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.Box(outputTex, width100, height100);
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.Space(4);
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        // Assign output channels
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}

