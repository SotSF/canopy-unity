using NodeEditorFramework;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates N radially-arranged copies of an input jet set. Each copy k is
/// rotated about the centerpoint by phase + k * (cover / N) and pushed
/// outward by radialOffset along its own placement angle, so:
///  - a single centered jet + radialOffset > 0 = ring of outward-facing jets
///  - cover &lt; 360 = a fan/trident over a partial arc
///  - radialOffset = 0 = a cluster at the centerpoint (e.g. klaxon: N=2, cover=360)
/// Hue range shifts each copy's dye color hue by hueRange * k / N.
/// </summary>
[Node(false, "Pattern/JetRadialArray")]
public class JetRadialArrayNode : TextureSynthNode
{
    public const string ID = "jetRadialArrayNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "JetRadialArray"; } }
    private Vector2 _DefaultSize = new Vector2(240, 300);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Jets", Direction.In, typeof(Jet[]), NodeSide.Left, 20)]
    public ValueConnectionKnob jetsInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Jet[]), NodeSide.Right, 20)]
    public ValueConnectionKnob jetsOutputKnob;

    [ValueConnectionKnob("Count", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob countKnob;
    [ValueConnectionKnob("Center X", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob centerXKnob;
    [ValueConnectionKnob("Center Y", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob centerYKnob;
    [ValueConnectionKnob("Cover", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob coverKnob;
    [ValueConnectionKnob("Phase", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob phaseKnob;
    [ValueConnectionKnob("Radial offset", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob radialOffsetKnob;
    [ValueConnectionKnob("Hue range", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob hueRangeKnob;

    public float count = 4;
    public float centerX = 0.5f;
    public float centerY = 0.5f;
    public float coverDegrees = 360;
    public float phaseDegrees = 0;
    public float radialOffset = 0.25f;
    public float hueRange = 0;

    [System.NonSerialized]
    private List<Jet> outputJets = new List<Jet>();

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref count, 1, 32, countKnob);
        FloatKnobOrSlider(ref centerX, 0, 1, centerXKnob);
        FloatKnobOrSlider(ref centerY, 0, 1, centerYKnob);
        FloatKnobOrSlider(ref coverDegrees, 0, 360, coverKnob);
        FloatKnobOrSlider(ref phaseDegrees, 0, 360, phaseKnob);
        FloatKnobOrSlider(ref radialOffset, 0, 0.7f, radialOffsetKnob);
        FloatKnobOrSlider(ref hueRange, 0, 1, hueRangeKnob);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        count = countKnob.connected() ? countKnob.GetValue<float>() : count;
        centerX = centerXKnob.connected() ? centerXKnob.GetValue<float>() : centerX;
        centerY = centerYKnob.connected() ? centerYKnob.GetValue<float>() : centerY;
        coverDegrees = coverKnob.connected() ? coverKnob.GetValue<float>() : coverDegrees;
        phaseDegrees = phaseKnob.connected() ? phaseKnob.GetValue<float>() : phaseDegrees;
        radialOffset = radialOffsetKnob.connected() ? radialOffsetKnob.GetValue<float>() : radialOffset;
        hueRange = hueRangeKnob.connected() ? hueRangeKnob.GetValue<float>() : hueRange;

        Jet[] input = jetsInputKnob.connected() ? jetsInputKnob.GetValue<Jet[]>() : null;
        int copies = Mathf.Max(1, Mathf.RoundToInt(count));

        outputJets.Clear();
        if (input != null)
        {
            Vector2 center = new Vector2(centerX, centerY);
            float spacing = (coverDegrees / copies) * Mathf.Deg2Rad;
            float phase = phaseDegrees * Mathf.Deg2Rad;

            for (int k = 0; k < copies; k++)
            {
                float theta = phase + k * spacing;
                float cosT = Mathf.Cos(theta);
                float sinT = Mathf.Sin(theta);
                Vector2 placementDir = new Vector2(cosT, sinT);

                foreach (Jet source in input)
                {
                    Jet jet = source.Clone();
                    Vector2 rel = source.position - center;
                    Vector2 rotated = new Vector2(
                        rel.x * cosT - rel.y * sinT,
                        rel.x * sinT + rel.y * cosT);
                    jet.position = center + rotated + radialOffset * placementDir;
                    jet.angle = source.angle + theta;

                    if (hueRange != 0)
                    {
                        float h, s, v;
                        Color.RGBToHSV(source.color, out h, out s, out v);
                        Color shifted = Color.HSVToRGB(Mathf.Repeat(h + hueRange * k / copies, 1), s, v);
                        shifted.a = source.color.a;
                        jet.color = shifted;
                    }
                    outputJets.Add(jet);
                }
            }
        }

        jetsOutputKnob.SetValue(outputJets.ToArray());
        return true;
    }
}
