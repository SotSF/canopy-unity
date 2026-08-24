using System;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Exposes every named Gradient in the scene's GradientManager as its own output port —
/// a palette rack: one node pipes any subset of the managed gradients into muxes and
/// samplers. Ports are name-keyed against the manager's list (added gradients grow new
/// ports, removed ones drop theirs; renames are new identities), with a live-baked
/// swatch per row. The Gradient objects stay in the manager, so inspector edits
/// propagate live to every consumer.
/// </summary>
[Node(false, "Pattern/GradientSelector")]
public class GradientSelectorNode : TickingNode
{
    public override string GetID => "GradientSelectorNode";
    public override string Title { get { return "GradientSelector"; } }

    private Vector2 _DefaultSize = new Vector2(220, 120);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(220, 60);
    public override bool AutoLayout => true;

    const int SwatchBakeWidth = 64;
    const int SwatchDisplayWidth = 72;

    [NonSerialized] private string[] cachedNames;
    [NonSerialized] private Gradient[] cachedGradients;
    [NonSerialized] private Texture2D[] swatches;
    [NonSerialized] private Color[] bakeBuffer;
    [NonSerialized] private int lastPulseFrame = -1;

    private void OnDestroy()
    {
        if (swatches != null)
        {
            foreach (var tex in swatches)
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
            swatches = null;
        }
    }

    ValueConnectionKnob FindPort(string name)
    {
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            if (dynamicConnectionPorts[i].name == name) return (ValueConnectionKnob)dynamicConnectionPorts[i];
        }
        return null;
    }

    // Name-keyed healing against the manager's current list: same-name ports keep their
    // connections, orphans are deleted, new names grow ports. Only runs while the manager
    // is live — serialized ports (and their wires) are never touched in edit mode or
    // before the manager exists.
    void ReconcilePorts()
    {
        for (int i = dynamicConnectionPorts.Count - 1; i >= 0; i--)
        {
            bool wanted = false;
            for (int n = 0; n < cachedNames.Length; n++)
            {
                if (cachedNames[n] == dynamicConnectionPorts[i].name) { wanted = true; break; }
            }
            if (!wanted)
            {
                dynamicConnectionPorts[i].ClearConnections();
                DeleteConnectionPort(i);
            }
        }
        foreach (var name in cachedNames)
        {
            if (FindPort(name) == null)
            {
                CreateValueConnectionKnob(new ValueConnectionKnobAttribute(
                    name, Direction.Out, typeof(Gradient), NodeSide.Right));
            }
        }
    }

    void RefreshFromManager()
    {
        var manager = GradientManager.Instance;
        if (manager == null) return;
        cachedNames = manager.GradientNames;
        cachedGradients = new Gradient[cachedNames.Length];
        for (int i = 0; i < cachedNames.Length; i++)
        {
            cachedGradients[i] = manager.Get(cachedNames[i]);
        }
    }

    // Rebaked every frame: gradients are edited live in the inspector while performing,
    // and N × 64 Evaluate calls is nothing
    void RebakeSwatches()
    {
        int count = cachedGradients != null ? cachedGradients.Length : 0;
        if (swatches == null || swatches.Length != count)
        {
            if (swatches != null)
            {
                foreach (var tex in swatches)
                {
                    if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            swatches = new Texture2D[count];
        }
        if (bakeBuffer == null || bakeBuffer.Length != SwatchBakeWidth)
        {
            bakeBuffer = new Color[SwatchBakeWidth];
        }
        for (int i = 0; i < count; i++)
        {
            if (cachedGradients[i] == null) continue;
            if (swatches[i] == null)
            {
                swatches[i] = GradientManager.CreateSwatchTexture(SwatchBakeWidth);
            }
            GradientManager.BakeSwatch(cachedGradients[i], swatches[i], bakeBuffer);
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode for GradientManager gradients");
        }
        else if (cachedNames == null || cachedNames.Length == 0)
        {
            GUILayout.Label("No gradients in GradientManager");
        }
        else
        {
            for (int i = 0; i < cachedNames.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(cachedNames[i], GUILayout.Width(90));
                if (swatches != null && i < swatches.Length && swatches[i] != null)
                {
                    GUILayout.Box(swatches[i], GUILayout.Width(SwatchDisplayWidth), GUILayout.Height(14));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                FindPort(cachedNames[i])?.SetPosition();
            }
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (Time.frameCount != lastPulseFrame && Application.isPlaying && GradientManager.Instance != null)
        {
            lastPulseFrame = Time.frameCount;
            RefreshFromManager();
            ReconcilePorts();
            RebakeSwatches();
        }
        if (cachedNames != null)
        {
            for (int i = 0; i < cachedNames.Length; i++)
            {
                FindPort(cachedNames[i])?.SetValue(cachedGradients[i]);
            }
        }
        return true;
    }
}
