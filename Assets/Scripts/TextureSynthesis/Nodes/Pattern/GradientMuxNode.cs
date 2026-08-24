using System;
using System.Linq;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Gradient multiplexer, following the SignalMux/TextureMux shape: any number of
/// Gradient inputs (one open slot always waits at the bottom — connecting it spawns
/// the next), an `Activate` button per input, and a `control` event pulse that cycles
/// through the connected inputs. Each input row shows a live-baked swatch of what's
/// arriving, and the active gradient passes through to the output.
/// Cycle pulses and port reconciliation run in the once-per-frame calc tick, so
/// OnNodeChange re-runs can't double-advance and GUI structure never mutates mid-pass.
/// </summary>
[Node(false, "Pattern/GradientMux")]
public class GradientMuxNode : TickingNode
{
    public override string GetID => "GradientMuxNode";
    public override string Title { get { return "GradientMux"; } }

    private Vector2 _DefaultSize = new Vector2(220, 120);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(220, 60);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("control", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob controlKnob;

    [ValueConnectionKnob("gradient", Direction.Out, typeof(Gradient), NodeSide.Right)]
    public ValueConnectionKnob gradientKnob;

    public int activeIndex = 0;

    const int SwatchBakeWidth = 64;
    const int SwatchDisplayWidth = 60;

    [NonSerialized] private Texture2D[] swatches;
    [NonSerialized] private Color[] bakeBuffer;
    [NonSerialized] private Gradient activeGradient;
    // GUI latches intent; ports, selection, and cycling change only in DoCalc's
    // once-per-frame tick
    [NonSerialized] private int pendingActivate = -1;
    [NonSerialized] private int lastPulseFrame = -1;

    int ConnectedCount => dynamicConnectionPorts.Count(port => port.connected());

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

    // Keep connected ports compacted at the front with exactly one open slot at the
    // bottom. Runs in the calc tick, never during a GUI pass.
    void ReconcilePorts()
    {
        // Delete unconnected ports except the last one (backwards: deletion shifts indices)
        for (int i = dynamicConnectionPorts.Count - 2; i >= 0; i--)
        {
            if (!dynamicConnectionPorts[i].connected())
            {
                DeleteConnectionPort(i);
                if (activeIndex > i) activeIndex--;
                else if (activeIndex == i) activeIndex = 0;
            }
        }
        // Ensure the one open slot (also creates the first slot on a fresh node)
        int count = dynamicConnectionPorts.Count;
        if (count == 0 || dynamicConnectionPorts[count - 1].connected())
        {
            CreateValueConnectionKnob(new ValueConnectionKnobAttribute(
                "Add input", Direction.In, typeof(Gradient), NodeSide.Left));
        }
    }

    void RebakeSwatches()
    {
        int rows = dynamicConnectionPorts.Count - 1; // exclude the open slot
        if (swatches == null || swatches.Length != rows)
        {
            if (swatches != null)
            {
                foreach (var tex in swatches)
                {
                    if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            swatches = new Texture2D[Mathf.Max(rows, 0)];
        }
        if (bakeBuffer == null || bakeBuffer.Length != SwatchBakeWidth)
        {
            bakeBuffer = new Color[SwatchBakeWidth];
        }
        for (int i = 0; i < rows; i++)
        {
            var gradient = ((ValueConnectionKnob)dynamicConnectionPorts[i]).GetValue<Gradient>();
            if (gradient == null) continue;
            if (swatches[i] == null)
            {
                swatches[i] = GradientManager.CreateSwatchTexture(SwatchBakeWidth);
            }
            GradientManager.BakeSwatch(gradient, swatches[i], bakeBuffer);
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        controlKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        gradientKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        int rows = dynamicConnectionPorts.Count - 1;
        for (int i = 0; i < rows; i++)
        {
            GUILayout.BeginHorizontal();
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            port.SetPosition();
            GUILayout.Space(4);
            if (swatches != null && i < swatches.Length && swatches[i] != null)
            {
                GUILayout.Box(swatches[i], GUILayout.Width(SwatchDisplayWidth), GUILayout.Height(14));
            }
            else
            {
                GUILayout.Label("—", GUILayout.Width(SwatchDisplayWidth));
            }
            GUILayout.Space(4);
            if (i == activeIndex)
            {
                GUILayout.Label("[ Active ]");
            }
            else if (GUILayout.Button("Activate", GUILayout.ExpandWidth(false)))
            {
                pendingActivate = i;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        if (dynamicConnectionPorts.Count > 0)
        {
            ((ValueConnectionKnob)dynamicConnectionPorts[dynamicConnectionPorts.Count - 1]).DisplayLayout();
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (Time.frameCount != lastPulseFrame)
        {
            lastPulseFrame = Time.frameCount;
            ReconcilePorts();

            int connected = ConnectedCount;
            if (pendingActivate >= 0 && pendingActivate < connected)
            {
                activeIndex = pendingActivate;
            }
            pendingActivate = -1;
            if (controlKnob != null && controlKnob.connected() && controlKnob.GetValue<bool>() && connected > 0)
            {
                activeIndex = (activeIndex + 1) % connected;
            }
            if (connected > 0)
            {
                activeIndex = Mathf.Clamp(activeIndex, 0, connected - 1);
                activeGradient = ((ValueConnectionKnob)dynamicConnectionPorts[activeIndex]).GetValue<Gradient>();
            }
            else
            {
                activeGradient = null;
            }
            RebakeSwatches();
        }

        gradientKnob.SetValue(activeGradient);
        return true;
    }
}
