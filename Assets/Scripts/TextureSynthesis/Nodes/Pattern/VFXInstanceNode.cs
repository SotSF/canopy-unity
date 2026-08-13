using NodeEditorFramework;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry-backed successor to VFXGraphNode: binds a VFX prefab from VFXRegistry
/// instead of a hand-configured scene object. Each node gets its own instance with
/// its own render texture, so multiple nodes can run the same base VFX with
/// independent parameters.
/// </summary>
[Node(false, "Pattern/VFXInstance")]
public class VFXInstanceNode : DynamicPatternNode
{
    public override string GetID => "VFXInstance";
    public override string Title { get { return "VFXInstance"; } }

    public string effectName = "";
    public bool vfxBound = false;

    private VFXInstance instance;
    private Dictionary<string, Texture> lastTexInputs;
    private int selectedVfxIdx = 0;

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
            DeleteConnectionPort(port);
        }
    }

    protected override void TopGUI()
    {
        if (vfxBound) return;
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to bind a VFX");
            return;
        }
        var names = VFXRegistry.Instance.EffectNames;
        if (names.Length == 0)
        {
            GUILayout.Label("No effect prefabs registered");
            return;
        }
        GUILayout.BeginVertical();
        GUILayout.Label("VFX:");
        selectedVfxIdx = Mathf.Clamp(selectedVfxIdx, 0, names.Length - 1);
        selectedVfxIdx = GUILayout.SelectionGrid(selectedVfxIdx, names, 1, LeftAlignedButton);
        GUILayout.Space(8);
        if (GUILayout.Button("Load VFX"))
        {
            effectName = names[selectedVfxIdx];
            CleanExistingConnections();
            vfxBound = true;
            DoInit();
        }
        GUILayout.Space(4);
        GUILayout.EndVertical();
    }

    protected override void SetSize()
    {
        if (!vfxBound && Application.isPlaying)
        {
            // Unbound: label + one row per VFX choice + load button + output image box
            int optionCount = VFXRegistry.Instance.EffectNames.Length;
            _DefaultSize = new Vector2(220, 230 + optionCount * 30);
        }
        else
        {
            base.SetSize();
        }
    }

    private void EnableFx()
    {
        instance = VFXRegistry.Instance.CreateInstance(this, effectName) as VFXInstance;
        if (instance == null || instance.effect == null)
        {
            Debug.LogError($"VFXInstanceNode: failed to bind '{effectName}', unbinding.");
            DisableFx();
            return;
        }
        outputTex = instance.OutputTexture;
        foreach (var prop in instance.GetExposedProperties())
        {
            inputPortNames.Add(prop.name);
            inputPortTypes.Add(prop.type);
            if (typeof(Texture).IsAssignableFrom(prop.type))
            {
                lastTexInputs[prop.name] = null;
            }
        }
    }

    private void DisableFx()
    {
        vfxBound = false;
        effectName = "";
        VFXRegistry.Instance?.ReleaseInstance(this);
        instance = null;
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

    protected override void OnDelete()
    {
        if (vfxBound && Application.isPlaying)
        {
            VFXRegistry.Instance?.ReleaseInstance(this);
        }
    }

    public override float GetPortPropValue(string portName)
    {
        if (instance != null && instance.effect != null && instance.effect.HasFloat(portName))
        {
            return instance.effect.GetFloat(portName);
        }
        // DynamicPatternNode.NodeGUI catches this and falls back to the port's own value
        throw new NotImplementedException();
    }

    public override bool DoCalc()
    {
        if (!vfxBound || instance == null || instance.effect == null)
        {
            textureOutputKnob.SetValue<Texture>(outputTex);
            return true;
        }
        var effect = instance.effect;
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.connections.Count == 0) continue;
            var portType = port.valueType;
            var propName = inputPortNames[i];
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
            else
            {
                Debug.LogWarning($"Unsupported type {portType} for VFX input {propName}.");
            }
        }
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
