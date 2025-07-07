
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

    public bool vfxBound = false;
    public string[] visualEffectNames = new string[] { "VFXCam", "TrailCam", "TorusCam" };

    public void InitBuffers()
    {
        inputPortNames = new List<string>();
        inputPortTypes = new List<Type>();
        lastTexInputs = new Dictionary<string, Texture>();
    }

    public override void DoInit()
    {
        InitBuffers();
        if (!vfxBound)
        {
            CleanExistingConnections();
            return;
        }
        EnableFx();
    }

    public void CleanExistingConnections()
    {
        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            var port = dynamicConnectionPorts[i];
            port.ClearConnections();
            // This is the correct way, not dynamicConnectionPorts.RemoveAt(i)
            // TODO: Fix all other instances of dynamicConnectionPorts.RemoveAt(i) ?
            DeleteConnectionPort(port);
        }
    }

    private int selectedVfxIdx = 0;
    protected override void TopGUI()
    {
        if (vfxBound) return;
        GUILayout.BeginHorizontal();
        GUILayout.Label("VFX Name:");
        selectedVfxIdx = GUILayout.SelectionGrid(selectedVfxIdx, visualEffectNames, 1);
        if (GUILayout.Button("Load VFX"))
        {
            gameObjectName = visualEffectNames[selectedVfxIdx];
            CleanExistingConnections();
            vfxBound = true;
            DoInit();
        }
        GUILayout.EndHorizontal();
    }

    private void EnableFx()
    {
        sceneObj = GameObject.Find(gameObjectName);
        cam = sceneObj.GetComponentsInChildren<Camera>(true).First();
        cam.gameObject.SetActive(true);
        outputTex = cam.targetTexture;
        effect = sceneObj.GetComponentsInChildren<VisualEffect>().First();
        effect.Play();
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
            //Debug.Log($"Exposed property: {prop.name}");
        }
    }

    private void DisableFx()
    {
        vfxBound = false;
        gameObjectName = "";
        effect.Stop();
        cam.gameObject.SetActive(false);
        outputTex = null;
        InitBuffers();
        CleanExistingConnections();
    }

    protected override void BottomGUI()
    {
        if (!vfxBound) return;
        if (GUILayout.Button("Unbind VFX"))
        {
            DisableFx();
        }
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

