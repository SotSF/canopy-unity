using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Pattern/JetInstance")]
public class JetInstanceNode : TextureSynthNode
{
    public const string ID = "jetInstanceNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "JetInstance"; } }
    private Vector2 _DefaultSize = new Vector2(240, 300);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Jets", Direction.Out, typeof(Jet[]), NodeSide.Right, 20)]
    public ValueConnectionKnob jetsOutputKnob;

    [ValueConnectionKnob("X", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob posXKnob;
    [ValueConnectionKnob("Y", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob posYKnob;
    [ValueConnectionKnob("Angle", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob angleKnob;
    [ValueConnectionKnob("Intensity", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob intensityKnob;
    [ValueConnectionKnob("Width", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob widthKnob;
    [ValueConnectionKnob("Reach", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob reachKnob;
    [ValueConnectionKnob("Spread", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob spreadKnob;
    [ValueConnectionKnob("Color", Direction.In, typeof(Vector4), NodeSide.Left)]
    public ValueConnectionKnob colorKnob;

    public float posX = 0.5f;
    public float posY = 0.5f;
    public float angleDegrees = 0;
    public float intensity = 1;
    public float jetWidth = 0.04f;
    public float jetReach = 0.35f;
    public float jetSpread = 0.15f;

    [System.NonSerialized]
    private Jet[] output = new Jet[1] { new Jet() };

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref posX, 0, 1, posXKnob);
        FloatKnobOrSlider(ref posY, 0, 1, posYKnob);
        FloatKnobOrSlider(ref angleDegrees, 0, 360, angleKnob);
        FloatKnobOrSlider(ref intensity, 0, 4, intensityKnob);
        FloatKnobOrSlider(ref jetWidth, 0.005f, 0.25f, widthKnob);
        FloatKnobOrSlider(ref jetReach, 0.02f, 1, reachKnob);
        FloatKnobOrSlider(ref jetSpread, 0, 1.4f, spreadKnob);
        GUILayout.BeginHorizontal();
        colorKnob.DisplayLayout(new GUIContent("Dye color", "Color emitted at the jet nozzle (white if unconnected)"));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        posX = posXKnob.connected() ? posXKnob.GetValue<float>() : posX;
        posY = posYKnob.connected() ? posYKnob.GetValue<float>() : posY;
        angleDegrees = angleKnob.connected() ? angleKnob.GetValue<float>() : angleDegrees;
        intensity = intensityKnob.connected() ? intensityKnob.GetValue<float>() : intensity;
        jetWidth = widthKnob.connected() ? widthKnob.GetValue<float>() : jetWidth;
        jetReach = reachKnob.connected() ? reachKnob.GetValue<float>() : jetReach;
        jetSpread = spreadKnob.connected() ? spreadKnob.GetValue<float>() : jetSpread;

        Jet jet = output[0];
        jet.position = new Vector2(posX, posY);
        jet.angle = angleDegrees * Mathf.Deg2Rad;
        jet.intensity = intensity;
        jet.width = jetWidth;
        jet.reach = jetReach;
        jet.spread = jetSpread;
        jet.color = colorKnob.connected() ? (Color)colorKnob.GetValue<Vector4>() : Color.white;

        jetsOutputKnob.SetValue(output);
        return true;
    }
}
