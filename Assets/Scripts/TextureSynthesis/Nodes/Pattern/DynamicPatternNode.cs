using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


abstract public class DynamicPatternNode : TickingNode
{
    protected Vector2 _DefaultSize = new Vector2(250, 100);

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
        for (int i = dynamicConnectionPorts.Count; i < inputPortNames.Count; i++)
        {
            var portSide = (typeof(Texture)).IsAssignableFrom(inputPortTypes[i]) ? NodeSide.Top : NodeSide.Left;
            ValueConnectionKnobAttribute inputKnobAttribs = new ValueConnectionKnobAttribute(inputPortNames[i], Direction.In, inputPortTypes[i], portSide);
            CreateValueConnectionKnob(inputKnobAttribs);
        }
        SetSize();
    }

    /// <summary>
    /// Heals serialized dynamic ports against the freshly-built inputPortNames/inputPortTypes:
    /// ports whose name+type still exist are KEPT (connections preserved) even if the source's
    /// property order changed; orphans are deleted; missing ports are created. Afterwards
    /// dynamicConnectionPorts is ordered exactly like inputPortNames, so the index-parallel
    /// GUI/calc code stays valid. Call after repopulating the name/type buffers on bind/load —
    /// without this, adding/removing exposed properties on a bound source desyncs the lists
    /// and index accesses kill the whole canvas.
    /// </summary>
    protected void ReconcileDynamicPorts()
    {
        // One keeper per still-existing name+type. Canvases saved by older code can hold
        // duplicate same-named ports (the old index-based SetupPorts appended them whenever
        // the property list drifted), so prefer a duplicate that actually carries a wire
        var keepers = new Dictionary<string, ValueConnectionKnob>();
        foreach (ValueConnectionKnob port in dynamicConnectionPorts)
        {
            int idx = inputPortNames.IndexOf(port.name);
            if (idx < 0 || inputPortTypes[idx] != port.valueType) continue;
            if (!keepers.TryGetValue(port.name, out var current)
                || (current.connections.Count == 0 && port.connections.Count > 0))
            {
                keepers[port.name] = port;
            }
        }
        // Delete everything else, saying which wires were lost so they can be redone
        foreach (ValueConnectionKnob port in dynamicConnectionPorts.ToList())
        {
            if (keepers.TryGetValue(port.name, out var keeper) && keeper == port) continue;
            if (port.connections.Count > 0)
            {
                int idx = inputPortNames.IndexOf(port.name);
                string reason = idx < 0 ? "no longer exists in its source"
                    : inputPortTypes[idx] != port.valueType ? $"changed type ({port.valueType.Name} → {inputPortTypes[idx].Name})"
                    : "was a duplicate left by an older save";
                string from = port.connections[0].body != null ? port.connections[0].body.Title : "?";
                Debug.LogWarning($"{Title}: input '{port.name}' {reason}; removed its connection from {from}.");
            }
            // Silent: this runs inside DoInit, and connection callbacks would recalculate this
            // node mid-init (nothing downstream needs notifying of a structural rebuild)
            port.ClearConnections(true);
            DeleteConnectionPort(port);
        }
        // Rebuild in inputPortNames order: keepers slot back in, missing ports get created
        // (CreateValueConnectionKnob appends, so creation order = list order)
        dynamicConnectionPorts.Clear();
        for (int j = 0; j < inputPortNames.Count; j++)
        {
            if (keepers.TryGetValue(inputPortNames[j], out var keep))
            {
                dynamicConnectionPorts.Add(keep);
            }
            else
            {
                var portSide = (typeof(Texture)).IsAssignableFrom(inputPortTypes[j]) ? NodeSide.Top : NodeSide.Left;
                CreateValueConnectionKnob(new ValueConnectionKnobAttribute(inputPortNames[j], Direction.In, inputPortTypes[j], portSide));
            }
        }
        ConnectionPortManager.UpdateRepresentativePortLists(this);
        SetSize();
    }

    int signalPorts => dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).side == NodeSide.Left).Count();
    int texPorts => dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).side == NodeSide.Top).Count();
    protected virtual void SetSize()
    {
        _DefaultSize = new Vector2(
            (1 + texPorts) * 32 + 168,
            // Texture input ports render a label + preview row that needs vertical room too
            (1 + signalPorts) * 25 + 150 + (texPorts > 0 ? 55 : 0)
        );
    }

    static GUIStyle _leftAlignedButton;
    protected static GUIStyle LeftAlignedButton
    {
        get
        {
            if (_leftAlignedButton == null)
            {
                _leftAlignedButton = new GUIStyle(GUI.skin.button);
                _leftAlignedButton.alignment = TextAnchor.MiddleLeft;
            }
            return _leftAlignedButton;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _leftAlignedButton = null;
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
                // port.name, never inputPortNames[i]: serialized ports can outnumber the
                // rebuilt name buffers when a bound source gained/lost exposed properties,
                // and an index crash here kills the entire canvas (RTNodeEditor discards it)
                var portName = port.name;
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
                var portName = port.name; // see tex-port loop: index-independent on purpose
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
                    catch (NotImplementedException)
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

