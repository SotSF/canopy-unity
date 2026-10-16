
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
    // Unsupported input types warn once per port, not every tick
    [NonSerialized] private HashSet<string> warnedUnsupported = new HashSet<string>();

    public bool vfxBound = false;
    public string[] visualEffectNames = new string[] { "VFXCam", "TrailCam", "TorusCam", "GoldenSpiralCam" };

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

    // GameObject.Find skips inactive objects, and unused VFX rigs under VFXManager are kept
    // inactive (e.g. GoldenSpiralCam), so search inactive scene objects too
    static GameObject FindSceneObject(string name)
    {
        var active = GameObject.Find(name);
        if (active != null) return active;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name) return t.gameObject;
        }
        return null;
    }

    private void EnableFx()
    {
        sceneObj = FindSceneObject(gameObjectName);
        cam = sceneObj != null ? sceneObj.GetComponentsInChildren<Camera>(true).FirstOrDefault() : null;
        effect = sceneObj != null ? sceneObj.GetComponentsInChildren<VisualEffect>(true).FirstOrDefault() : null;
        if (cam == null || effect == null)
        {
            // Stay bound (gameObjectName is user data) but inert; the node shows the problem.
            // Throwing here would fail DoInit, which retries — and throws — every tick
            Debug.LogError($"VFXGraph: no VFX rig '{gameObjectName}' with a Camera and VisualEffect in the scene.");
            cam = null;
            effect = null;
            return;
        }
        sceneObj.SetActive(true);
        cam.gameObject.SetActive(true);
        outputTex = cam.targetTexture;
        effect.Play();
        // Query exposed properties from the VFX Graph
        List<VFXExposedProperty> exposedProperties = new List<VFXExposedProperty>(); ;
        effect.visualEffectAsset.GetExposedProperties(exposedProperties);
        foreach (var prop in exposedProperties)
        {
            inputPortNames.Add(prop.name);
            inputPortTypes.Add(prop.type);
            if (typeof(Texture).IsAssignableFrom(prop.type))
            {
                lastTexInputs[prop.name] = null;
            }
            //Debug.Log($"Exposed property: {prop.name}");
        }
        // Heal serialized ports against the graph's current exposed properties: same name +
        // type keeps its wire, a property whose type changed gets a fresh port (with a warning
        // if it was connected) instead of pushing the old type at it every frame
        ReconcileDynamicPorts();
    }

    private void DisableFx()
    {
        vfxBound = false;
        gameObjectName = "";
        if (effect != null) effect.Stop();
        if (cam != null) cam.gameObject.SetActive(false);
        effect = null;
        cam = null;
        outputTex = null;
        InitBuffers();
        CleanExistingConnections();
    }

    protected override void BottomGUI()
    {
        if (!vfxBound) return;
        if (effect == null && Application.isPlaying)
        {
            GUILayout.Label($"⚠ VFX rig '{gameObjectName}' not found");
        }
        if (GUILayout.Button("Unbind VFX"))
        {
            DisableFx();
        }
    }

    public override float GetPortPropValue(string portName)
    {
        if (effect != null && effect.HasFloat(portName))
        {
            return effect.GetFloat(portName);
        }
        // DynamicPatternNode.NodeGUI catches this and falls back to the port's own value
        throw new NotImplementedException();
    }

    public override bool DoCalc()
    {
        if (!vfxBound || effect == null)
        {
            textureOutputKnob.SetValue<Texture>(outputTex);
            return true;
        }
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.connections.Count == 0) continue;
            var portType = port.valueType;
            var propName = port.name; // index-independent: survives port/property drift
            if (portType == typeof(float))
            {
                effect.SetFloat(propName, port.GetValue<float>());
            }
            else if (portType == typeof(int))
            {
                effect.SetInt(propName, port.GetValue<int>());
            }
            else if (portType == typeof(bool))
            {
                effect.SetBool(propName, port.GetValue<bool>());
            }
            else if (portType == typeof(Vector2))
            {
                effect.SetVector2(propName, port.GetValue<Vector2>());
            }
            else if (portType == typeof(Vector3))
            {
                effect.SetVector3(propName, port.GetValue<Vector3>());
            }
            else if (portType == typeof(Vector4))
            {
                effect.SetVector4(propName, port.GetValue<Vector4>());
            }
            else if (portType == typeof(Gradient))
            {
                var val = port.GetValue<Gradient>();
                if (val != null) effect.SetGradient(propName, val);
            }
            else if (typeof(Texture).IsAssignableFrom(portType))
            {
                Texture val = port.GetValue<Texture>();
                lastTexInputs.TryGetValue(propName, out var last);
                if (val != null && val != last)
                {
                    effect.SetTexture(propName, val);
                    lastTexInputs[propName] = val;
                }
            }
            else if (warnedUnsupported.Add(propName))
            {
                Debug.LogWarning($"VFXGraph: unsupported type {portType.Name} for input '{propName}'; it won't be driven.");
            }
        }
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}

