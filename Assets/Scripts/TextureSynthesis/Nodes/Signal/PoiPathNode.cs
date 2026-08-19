using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;
using Smartsticks = Lightsale.Products.Smartsticks;

/// <summary>
/// Evaluates a first-order poi path (predefined epicycles) as a two-poi pattern set,
/// outputting left- and right-hand positions as Vector3s. Timing sets the hands' relative
/// offset (Same = 0, Quarter = 0.25, Split = 0.5) applied to the right hand's parametric t,
/// so arm and poi shift together like a real pattern pair. T can be driven externally
/// (in revolutions, e.g. a Timeline's time output divided by period) or the node free-runs
/// at one revolution per `period` seconds.
/// </summary>
[Node(false, "Signal/PoiPath")]
public class PoiPathNode : TickingNode
{
    public override string GetID => "PoiPathNode";
    public override string Title { get { return "PoiPath"; } }

    private Vector2 _DefaultSize = new Vector2(230, 250);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("T", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob tKnob;

    [ValueConnectionKnob("period", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob periodKnob;

    [ValueConnectionKnob("armLen", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob armLengthKnob;

    [ValueConnectionKnob("poiLen", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob poiLengthKnob;

    [ValueConnectionKnob("position", Direction.Out, typeof(Vector3), NodeSide.Right)]
    public ValueConnectionKnob positionKnob;

    [ValueConnectionKnob("positionR", Direction.Out, typeof(Vector3), NodeSide.Right)]
    public ValueConnectionKnob positionRKnob;

    public RadioButtonSet spin = new RadioButtonSet(0, "InSpin", "AntiSpin");
    public RadioButtonSet timing = new RadioButtonSet(0, "Same", "Quarter", "Split");
    public RadioButtonSet direction = new RadioButtonSet(0, "Clockwise", "Anticlockwise");
    public int count = 3;
    public float period = 4f;
    public float armLength = 1f;
    public float poiLength = 1f;
    public float armPhase = 0f;
    public float poiPhase = 0f;
    public float t = 0f;

    [NonSerialized] private Smartsticks.FirstOrderPoiPath path;
    [NonSerialized] private Vector3 lastPosition;

    static float TimingPhase(RadioButtonSet timing)
    {
        if (timing.IsSelected("Quarter")) return 0.25f;
        if (timing.IsSelected("Split")) return 0.5f;
        return 0f;
    }

    public override void DoInit()
    {
        path = new Smartsticks.FirstOrderPoiPath();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        RadioButtonsHorizontal(spin);
        RadioButtonsHorizontal(timing);
        RadioButtonsHorizontal(direction);

        GUILayout.BeginHorizontal();
        GUILayout.Label("count", GUILayout.Width(60));
        count = RTEditorGUI.IntSlider(count, 1, 9);
        GUILayout.EndHorizontal();

        FloatKnobOrField("period", ref period, periodKnob);

        FloatKnobOrSlider(ref armLength, 0f, 4f, armLengthKnob);
        FloatKnobOrSlider(ref poiLength, 0f, 4f, poiLengthKnob);

        GUILayout.BeginHorizontal();
        GUILayout.Label("arm phase", GUILayout.Width(60));
        armPhase = RTEditorGUI.Slider(armPhase, 0f, 1f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("poi phase", GUILayout.Width(60));
        poiPhase = RTEditorGUI.Slider(poiPhase, 0f, 1f);
        GUILayout.EndHorizontal();

        // Knob fields can be null when drawn via the Node Inspector on a node whose ports
        // haven't been rebuilt yet (e.g. a canvas saved before positionR existed)
        GUILayout.BeginHorizontal();
        if (tKnob != null) tKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"L({lastPosition.x:0.00}, {lastPosition.y:0.00})");
        GUILayout.FlexibleSpace();
        if (positionKnob != null) positionKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (positionRKnob != null) positionRKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (path == null) path = new Smartsticks.FirstOrderPoiPath();
        if (periodKnob != null && periodKnob.connected())
        {
            period = periodKnob.GetValue<float>();
        }
        if (armLengthKnob != null && armLengthKnob.connected())
        {
            armLength = armLengthKnob.GetValue<float>();
        }
        if (poiLengthKnob != null && poiLengthKnob.connected())
        {
            poiLength = poiLengthKnob.GetValue<float>();
        }
        if (tKnob != null && tKnob.connected())
        {
            t = tKnob.GetValue<float>();
        }
        else
        {
            t = Mathf.Repeat(t + Time.deltaTime / Mathf.Max(period, 0.01f), 1f);
        }

        path.count = Mathf.Max(count, 1);
        path.armPhase = armPhase;
        path.poiPhase = poiPhase;
        path.spin = spin.IsSelected("AntiSpin") ? Smartsticks.Spin.AntiSpin : Smartsticks.Spin.InSpin;
        path.direction = direction.IsSelected("Anticlockwise")
            ? Smartsticks.Direction.Anticlockwise
            : Smartsticks.Direction.Clockwise;

        // Right hand runs the same path offset by the timing fraction in parametric t,
        // so arm and poi shift together (split-time even-count patterns land correctly)
        lastPosition = path.PositionAtTime(t, armLength, poiLength);
        if (positionKnob != null) positionKnob.SetValue(lastPosition);
        if (positionRKnob != null) positionRKnob.SetValue(path.PositionAtTime(t + TimingPhase(timing), armLength, poiLength));
        return true;
    }
}
