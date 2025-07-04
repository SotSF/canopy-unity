
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using UnityEngine.VFX.Utility;
using UnityEngine.VFX;
using System.Linq;
using NUnit.Framework;
using System.Collections.Generic;
using System;

[Node(false, "Pattern/VFXGraph")]
public class VFXGraphNode: DynamicPatternNode
{
    public override string GetID => "VFXGraph";
    public override string Title { get { return "VFXGraph"; } }
    private Vector2 _DefaultSize = new Vector2(250, 200);

    private Camera cam;
    private GameObject sceneObj;
    private VisualEffect effect;

    public string gameObjectName = "VFXCam";
    private Dictionary<string, Texture> lastTexInputs;

    public void initBuffers()
    {
        inputPortNames = new List<string>();
        inputPortTypes = new List<Type>();
        lastTexInputs = new Dictionary<string, Texture>();
    }

    public override void DoInit()
    {
        //vfxPrefab = Resources.Load<Transform>("Prefabs/");
        initBuffers();
        sceneObj = GameObject.Find(gameObjectName);
        cam = sceneObj.GetComponentsInChildren<Camera>().First();
        outputTex = cam.targetTexture;
        effect = sceneObj.GetComponentsInChildren<VisualEffect>().First();
        // Query exposed properties from the VFX Graph
        List<VFXExposedProperty> exposedProperties = new List<VFXExposedProperty>(); ;
        effect.visualEffectAsset.GetExposedProperties(exposedProperties);
        foreach (var prop in exposedProperties)
        {
            inputPortNames.Add(prop.name);
            inputPortTypes.Add(prop.type);
            if (prop.type == typeof(Texture))
            {
                lastTexInputs[prop.name] = null;
            }
            Debug.Log($"Exposed property: {prop.name}");
        }
    }

    public void CleanExistingConnections()
    {
        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            port.ClearConnections();
            dynamicConnectionPorts.RemoveAt(i);
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("VFX Name:");
        gameObjectName = GUILayout.TextField(gameObjectName);
        if (GUILayout.Button("Load VFX"))
        {
            CleanExistingConnections();
            DoInit();
        }
        GUILayout.EndHorizontal();
        base.NodeGUI();
    }

    public override float GetPortPropValue(string portName)
    {
        return effect.GetFloat(portName);
    }

    public override bool DoCalc()
    {
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            var portType = port.valueType;
            if (port.connections.Count > 0)
            {
                if (portType == typeof(float))
                {
                    float val = port.GetValue<float>();
                    effect.SetFloat(inputPortNames[i], val);
                }
                else if (portType == typeof(int))
                {
                    int val = port.GetValue<int>();
                    effect.SetInt(inputPortNames[i], val);
                }
                else if (portType == typeof(Texture))
                {
                    Texture val = port.GetValue<Texture>();
                    try
                    {
                        if (lastTexInputs[inputPortNames[i]] != val)
                        {
                            effect.SetTexture(inputPortNames[i], val);
                        }
                    }
                    catch
                    {

                    }
                }
                else
                {
                    Debug.LogWarning($"Unsupported type {portType} for VFX Graph input {inputPortNames[i]}.");
                }
            }
        }
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}

