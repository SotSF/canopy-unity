using NodeEditorFramework;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ShaderGraph counterpart to VFXInstanceNode: binds a shader graph from
/// ShaderGraphRegistry and exposes its "_LW_"-prefixed properties as input ports.
/// Each node gets its own material and render texture, so multiple nodes can run
/// the same graph with independent parameters.
/// </summary>
[Node(false, "Pattern/ShaderGraphInstance")]
public class ShaderGraphInstanceNode : DynamicPatternNode
{
    public override string GetID => "ShaderGraphInstance";
    public override string Title { get { return "ShaderGraphInstance"; } }

    public string effectName = "";
    public bool graphBound = false;

    private ShaderGraphInstance instance;
    private Dictionary<string, Texture> lastTexInputs;
    private int selectedGraphIdx = 0;

    public void InitBuffers()
    {
        inputPortNames = new List<string>();
        inputPortTypes = new List<Type>();
        lastTexInputs = new Dictionary<string, Texture>();
    }

    public override void DoInit()
    {
        InitBuffers();
        if (!graphBound)
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
        if (graphBound) return;
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to bind a ShaderGraph");
            return;
        }
        var names = ShaderGraphRegistry.Instance.EffectNames;
        if (names.Length == 0)
        {
            GUILayout.Label("No shader graphs registered");
            return;
        }
        GUILayout.BeginVertical();
        GUILayout.Label("Graph:");
        selectedGraphIdx = Mathf.Clamp(selectedGraphIdx, 0, names.Length - 1);
        selectedGraphIdx = GUILayout.SelectionGrid(selectedGraphIdx, names, 1, LeftAlignedButton);
        GUILayout.Space(8);
        if (GUILayout.Button("Load Graph"))
        {
            effectName = names[selectedGraphIdx];
            CleanExistingConnections();
            graphBound = true;
            DoInit();
        }
        GUILayout.Space(4);
        GUILayout.EndVertical();
    }

    protected override void SetSize()
    {
        if (!graphBound && Application.isPlaying)
        {
            // Unbound: label + one row per graph choice + load button + output image box
            int optionCount = ShaderGraphRegistry.Instance.EffectNames.Length;
            _DefaultSize = new Vector2(220, 230 + optionCount * 30);
        }
        else
        {
            base.SetSize();
        }
    }

    private void EnableFx()
    {
        instance = ShaderGraphRegistry.Instance.CreateInstance(this, effectName);
        if (instance == null || instance.material == null)
        {
            Debug.LogError($"ShaderGraphInstanceNode: failed to bind '{effectName}', unbinding.");
            DisableFx();
            return;
        }
        outputTex = instance.OutputTexture;
        foreach (var (propName, propType) in instance.GetExposedProperties())
        {
            inputPortNames.Add(propName);
            inputPortTypes.Add(propType);
            if (typeof(Texture).IsAssignableFrom(propType))
            {
                lastTexInputs[propName] = null;
            }
        }
        // Heal serialized ports against the current property set: same-name ports keep their
        // connections even if the graph gained/lost/reordered exposed properties since the save
        ReconcileDynamicPorts();
    }

    private void DisableFx()
    {
        graphBound = false;
        effectName = "";
        ShaderGraphRegistry.Instance?.ReleaseInstance(this);
        instance = null;
        outputTex = null;
        InitBuffers();
        CleanExistingConnections();
    }

    protected override void BottomGUI()
    {
        if (!graphBound) return;
        if (GUILayout.Button("Unbind Graph"))
        {
            DisableFx();
        }
    }

    protected override void OnDelete()
    {
        if (graphBound && Application.isPlaying)
        {
            ShaderGraphRegistry.Instance?.ReleaseInstance(this);
        }
    }

    public override float GetPortPropValue(string portName)
    {
        // DynamicPatternNode's GUI strips the _LW_ prefix before calling this
        var mat = instance?.material;
        if (mat != null)
        {
            if (mat.HasFloat(portName)) return mat.GetFloat(portName);
            var prefixed = ShaderGraphInstance.ExposedPrefix + portName;
            if (mat.HasFloat(prefixed)) return mat.GetFloat(prefixed);
        }
        // Falls back to the port's own value in DynamicPatternNode.NodeGUI
        throw new NotImplementedException();
    }

    public override bool DoCalc()
    {
        if (!graphBound || instance == null || instance.material == null)
        {
            textureOutputKnob.SetValue<Texture>(outputTex);
            return true;
        }
        var mat = instance.material;
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.connections.Count == 0) continue;
            var portType = port.valueType;
            var propName = port.name; // index-independent: survives port/property drift
            if (portType == typeof(float))
            {
                mat.SetFloat(propName, port.GetValue<float>());
            }
            else if (portType == typeof(int))
            {
                mat.SetInteger(propName, port.GetValue<int>());
            }
            else if (portType == typeof(Color))
            {
                mat.SetColor(propName, port.GetValue<Color>());
            }
            else if (typeof(Texture).IsAssignableFrom(portType))
            {
                Texture val = port.GetValue<Texture>();
                lastTexInputs.TryGetValue(propName, out var last);
                if (val != null && val != last)
                {
                    mat.SetTexture(propName, val);
                    lastTexInputs[propName] = val;
                }
            }
            else
            {
                Debug.LogWarning($"Unsupported type {portType} for ShaderGraph input {propName}.");
            }
        }
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
