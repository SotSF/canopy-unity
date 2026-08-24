using System;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Samples an incoming Gradient to a single color. Two modes (radio): `float` samples
/// at a position driven by the position port (or slider) — sweep it with an envelope
/// or LFO for palette animation; `random` picks a new random position on each `sample`
/// event pulse — wire a beat or MIDI hit for a fresh palette color per event.
/// Outputs both Color (ShaderGraph/UnityEffect color ports) and Vector4 (VFX exposed
/// colors), with a live swatch showing the gradient, the sample point, and the result.
/// </summary>
[Node(false, "Pattern/GradientSample")]
public class GradientSampleNode : TickingNode
{
    public override string GetID => "GradientSampleNode";
    public override string Title { get { return "GradientSample"; } }

    private Vector2 _DefaultSize = new Vector2(220, 150);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(220, 60);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("gradient", Direction.In, typeof(Gradient), NodeSide.Left)]
    public ValueConnectionKnob gradientKnob;

    [ValueConnectionKnob("position", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob positionKnob;

    [ValueConnectionKnob("sample", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob sampleKnob;

    [ValueConnectionKnob("color", Direction.Out, typeof(Color), NodeSide.Right)]
    public ValueConnectionKnob colorKnob;

    [ValueConnectionKnob("vec4", Direction.Out, typeof(Vector4), NodeSide.Right)]
    public ValueConnectionKnob vec4Knob;

    public RadioButtonSet mode = new RadioButtonSet(0, "float", "random");
    public float position = 0.5f;
    public float randomT = 0.5f; // survives save/load so the picked color is stable

    const int SwatchBakeWidth = 64;
    const float SwatchHeight = 14f;

    [NonSerialized] private Texture2D swatch;
    [NonSerialized] private Color[] bakeBuffer;
    [NonSerialized] private Gradient gradient;
    [NonSerialized] private Color sampledColor = Color.black;
    [NonSerialized] private float sampledT;
    [NonSerialized] private Rect swatchRect;
    // GUI latches intent; sampling and swatch baking run in the once-per-frame calc tick
    [NonSerialized] private bool pendingResample;
    [NonSerialized] private int lastPulseFrame = -1;

    private void OnDestroy()
    {
        if (swatch != null)
        {
            UnityEngine.Object.DestroyImmediate(swatch);
            swatch = null;
        }
    }

    public override void NodeGUI()
    {
        // Snapshot before the radios: a click mid-pass must not change this pass's structure
        bool floatMode = mode.IsSelected("float");

        GUILayout.BeginVertical();
        RadioButtonsHorizontal(mode);

        GUILayout.BeginHorizontal();
        gradientKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        colorKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (floatMode)
        {
            FloatKnobOrSlider(ref position, 0f, 1f, positionKnob);
        }
        else
        {
            sampleKnob.DisplayLayout();
            if (GUILayout.Button("Resample", GUILayout.Width(70)))
            {
                pendingResample = true;
            }
            GUILayout.Label($"t = {sampledT:0.00}");
        }
        GUILayout.FlexibleSpace();
        vec4Knob.DisplayLayout();
        GUILayout.EndHorizontal();

        // Swatch strip: the gradient, a marker at the sample point, and the result chip
        Rect r = GUILayoutUtility.GetRect(10f, SwatchHeight + 4f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            swatchRect = r;
        }
        if (Event.current.type == EventType.Repaint && swatchRect.width > 0f)
        {
            float chipWidth = 24f;
            var strip = new Rect(swatchRect.x + 4f, swatchRect.y + 2f,
                swatchRect.width - chipWidth - 12f, SwatchHeight);
            if (swatch != null)
            {
                GUI.DrawTexture(strip, swatch, ScaleMode.StretchToFill);
                float markerX = strip.x + Mathf.Clamp01(sampledT) * strip.width;
                FillRect(new Rect(markerX - 1f, strip.y - 2f, 2f, strip.height + 4f), Color.white);
            }
            FillRect(new Rect(strip.xMax + 4f, strip.y, chipWidth, SwatchHeight), sampledColor);
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    static void FillRect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public override bool DoCalc()
    {
        if (Time.frameCount != lastPulseFrame)
        {
            lastPulseFrame = Time.frameCount;
            gradient = gradientKnob != null && gradientKnob.connected()
                ? gradientKnob.GetValue<Gradient>()
                : null;

            if (mode.IsSelected("random"))
            {
                bool pulse = sampleKnob != null && sampleKnob.connected() && sampleKnob.GetValue<bool>();
                if (pulse || pendingResample)
                {
                    randomT = UnityEngine.Random.value;
                }
                sampledT = randomT;
            }
            else
            {
                float t = positionKnob != null && positionKnob.connected()
                    ? positionKnob.GetValue<float>()
                    : position;
                sampledT = Mathf.Clamp01(t);
            }
            pendingResample = false;

            sampledColor = gradient != null ? gradient.Evaluate(sampledT) : Color.black;

            // Rebaked per frame: gradient content is edited live in the inspector
            if (gradient != null)
            {
                if (swatch == null) swatch = GradientManager.CreateSwatchTexture(SwatchBakeWidth);
                if (bakeBuffer == null || bakeBuffer.Length != SwatchBakeWidth) bakeBuffer = new Color[SwatchBakeWidth];
                GradientManager.BakeSwatch(gradient, swatch, bakeBuffer);
            }
        }

        colorKnob.SetValue(sampledColor);
        vec4Knob.SetValue((Vector4)sampledColor);
        return true;
    }
}
