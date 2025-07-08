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
    private Vector2 _DefaultSize = new Vector2(250, 100);

    public override Vector2 DefaultSize => _DefaultSize;

     [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;

    protected RenderTexture outputTex;

    protected Vector2Int outputSize = Vector2Int.zero;

    protected List<string> inputPortNames;
    protected List<Type> inputPortTypes;

    public override void DoInit()
    {
        SetSize();
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
        //if (resized)
        //{
            SetSize();
        //}
    }

    int signalPorts => dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).side == NodeSide.Left).Count();
    int texPorts => dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).side == NodeSide.Top).Count();
    protected void SetSize()
    {

        _DefaultSize = new Vector2(
            (1 + texPorts) * 32 + 168,
            (1 + signalPorts) * 25 + 150
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
    GUILayoutOption width150 = GUILayout.Width(100);
    GUILayoutOption height25 = GUILayout.Height(25);
    GUILayoutOption height50 = GUILayout.Height(50);
    GUILayoutOption height100 = GUILayout.Height(100);

    public virtual float GetPortPropValue(string portName)
    {
        throw new NotImplementedException();
    }

    protected virtual void TopGUI()
    {
        // Override this method to add custom GUI elements below the texture input ports but above the signals
    }

    protected virtual void BottomGUI()
    {
        // Override this method to add custom GUI elements above the final output texture but below the signals
    }

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
                var portName = inputPortNames[i];
                if (portName.StartsWith("_LW_"))
                {
                    portName = portName.Substring("_LW_".Length);
                }
                GUILayout.Label(portName, width150);
                GUILayout.Space(4);
                GUILayout.Box(port.GetValue<Texture>(), width25, height25);
                port.SetPosition();
                GUILayout.EndVertical();
            }
        }
        GUILayout.EndHorizontal();

        TopGUI();

        // Draw signal ports up/down
        GUILayout.BeginVertical();
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.side == NodeSide.Left)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(2);
                var portName = inputPortNames[i];
                if (portName.StartsWith("_LW_"))
                {
                    portName = portName.Substring("_LW_".Length);
                }
                if (port.valueType == typeof(float))
                {
                    float val = 0;
                    try
                    {
                        val = GetPortPropValue(portName);
                    } 
                    catch (NotImplementedException ex)
                    {
                        val = port.GetValue<float>();
                    }
                    GUILayout.Label(string.Format($"{portName}: {val:0.00}"), GUILayout.ExpandWidth(true));
                }
                else
                {
                    GUILayout.Label(string.Format($"{portName}: {port.GetValue()}"), GUILayout.ExpandWidth(true));
                }
                port.SetPosition();
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndVertical();
        // End signal ports
        BottomGUI();

        // Draw rendered image
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.Box(outputTex, width100, height100);
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();


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

