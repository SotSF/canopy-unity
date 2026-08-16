using NodeEditorFramework;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameObject counterpart to VFXInstanceNode: binds a UnityEffectInstance prefab from
/// VFXRegistry and exposes its renderers' material properties and CanvasPortBinder
/// ports as input ports. Each node gets its own instance with its own render texture,
/// so multiple nodes can run the same rig with independent parameters.
/// </summary>
[Node(false, "Pattern/UnityEffectInstance")]
public class UnityEffectInstanceNode : DynamicPatternNode
{
    public override string GetID => "UnityEffectInstance";
    public override string Title { get { return "UnityEffectInstance"; } }

    public string effectName = "";
    public bool effectBound = false;

    private UnityEffectInstance instance;
    private Dictionary<string, Texture> lastTexInputs;
    private int selectedEffectIdx = 0;

    public void InitBuffers()
    {
        inputPortNames = new List<string>();
        inputPortTypes = new List<Type>();
        lastTexInputs = new Dictionary<string, Texture>();
    }

    public override void DoInit()
    {
        InitBuffers();
        if (!effectBound)
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
            DeleteConnectionPort(port);
        }
    }

    protected override void TopGUI()
    {
        if (effectBound) return;
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to bind an effect");
            return;
        }
        var names = VFXRegistry.Instance.GetEffectNames<UnityEffectInstance>();
        if (names.Length == 0)
        {
            GUILayout.Label("No UnityEffect prefabs registered");
            return;
        }
        GUILayout.BeginVertical();
        GUILayout.Label("Effect:");
        selectedEffectIdx = Mathf.Clamp(selectedEffectIdx, 0, names.Length - 1);
        selectedEffectIdx = GUILayout.SelectionGrid(selectedEffectIdx, names, 1, LeftAlignedButton);
        GUILayout.Space(8);
        if (GUILayout.Button("Load Effect"))
        {
            effectName = names[selectedEffectIdx];
            CleanExistingConnections();
            effectBound = true;
            DoInit();
        }
        GUILayout.Space(4);
        GUILayout.EndVertical();
    }

    protected override void SetSize()
    {
        if (!effectBound && Application.isPlaying)
        {
            // Unbound: label + one row per effect choice + load button + output image box
            int optionCount = VFXRegistry.Instance.GetEffectNames<UnityEffectInstance>().Length;
            _DefaultSize = new Vector2(220, 230 + optionCount * 30);
        }
        else
        {
            base.SetSize();
        }
    }

    private void EnableFx()
    {
        instance = VFXRegistry.Instance.CreateInstance(this, effectName) as UnityEffectInstance;
        if (instance == null)
        {
            Debug.LogError($"UnityEffectInstanceNode: failed to bind '{effectName}', unbinding.");
            DisableFx();
            return;
        }
        outputTex = instance.OutputTexture;
        var ports = new List<UnityEffectInstance.CanvasPort>();
        instance.GetCanvasPorts(ports); // renderer material properties + CanvasPortBinder ports
        foreach (var p in ports)
        {
            inputPortNames.Add(p.name);
            inputPortTypes.Add(p.type);
            if (typeof(Texture).IsAssignableFrom(p.type))
            {
                lastTexInputs[p.name] = null;
            }
        }
        // Heal serialized ports against the current property set: same-name ports keep their
        // connections even if the rig gained/lost/reordered exposed properties since the save
        ReconcileDynamicPorts();
    }

    private void DisableFx()
    {
        effectBound = false;
        effectName = "";
        VFXRegistry.Instance?.ReleaseInstance(this);
        instance = null;
        outputTex = null;
        InitBuffers();
        CleanExistingConnections();
    }

    protected override void BottomGUI()
    {
        if (!effectBound) return;
        if (GUILayout.Button("Unbind Effect"))
        {
            DisableFx();
        }
    }

    protected override void OnDelete()
    {
        if (effectBound && Application.isPlaying)
        {
            VFXRegistry.Instance?.ReleaseInstance(this);
        }
    }

    public override float GetPortPropValue(string portName)
    {
        if (instance != null && instance.TryGetMaterialFloat(portName, out float val))
        {
            return val;
        }
        // DynamicPatternNode.NodeGUI catches this and falls back to the port's own value
        throw new NotImplementedException();
    }

    public override bool DoCalc()
    {
        if (!effectBound || instance == null)
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
            // Binder ports (transforms etc.) take priority over same-named material properties
            if (portType == typeof(float))
            {
                float val = port.GetValue<float>();
                if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
            }
            else if (portType == typeof(int))
            {
                int val = port.GetValue<int>();
                if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
            }
            else if (portType == typeof(bool))
            {
                // Materials have no bool properties; bool ports only come from binders
                instance.TrySetBinderPort(propName, port.GetValue<bool>());
            }
            else if (portType == typeof(Color))
            {
                Color val = port.GetValue<Color>();
                if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
            }
            else if (portType == typeof(Vector2))
            {
                Vector2 val = port.GetValue<Vector2>();
                if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
            }
            else if (portType == typeof(Vector3))
            {
                Vector3 val = port.GetValue<Vector3>();
                if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
            }
            else if (portType == typeof(Vector4))
            {
                Vector4 val = port.GetValue<Vector4>();
                if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
            }
            else if (typeof(Texture).IsAssignableFrom(portType))
            {
                Texture val = port.GetValue<Texture>();
                lastTexInputs.TryGetValue(propName, out var last);
                if (val != null && val != last)
                {
                    if (!instance.TrySetBinderPort(propName, val)) instance.TrySetMaterialPort(propName, val);
                    lastTexInputs[propName] = val;
                }
            }
            else
            {
                Debug.LogWarning($"Unsupported type {portType} for UnityEffect input {propName}.");
            }
        }
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
