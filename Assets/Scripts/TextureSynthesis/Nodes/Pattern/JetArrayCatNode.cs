using NodeEditorFramework;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Concatenates up to four Jet[] inputs into a single array, in port order.
/// Unconnected inputs are skipped.
/// </summary>
[Node(false, "Pattern/JetArrayCat")]
public class JetArrayCatNode : TextureSynthNode
{
    public const string ID = "jetArrayCatNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "JetArrayCat"; } }
    private Vector2 _DefaultSize = new Vector2(150, 160);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("A", Direction.In, typeof(Jet[]), NodeSide.Left)]
    public ValueConnectionKnob aKnob;
    [ValueConnectionKnob("B", Direction.In, typeof(Jet[]), NodeSide.Left)]
    public ValueConnectionKnob bKnob;
    [ValueConnectionKnob("C", Direction.In, typeof(Jet[]), NodeSide.Left)]
    public ValueConnectionKnob cKnob;
    [ValueConnectionKnob("D", Direction.In, typeof(Jet[]), NodeSide.Left)]
    public ValueConnectionKnob dKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Jet[]), NodeSide.Right, 20)]
    public ValueConnectionKnob jetsOutputKnob;

    [System.NonSerialized]
    private List<Jet> outputJets = new List<Jet>();

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        aKnob.DisplayLayout();
        bKnob.DisplayLayout();
        cKnob.DisplayLayout();
        dKnob.DisplayLayout();
        GUILayout.Label(outputJets.Count + " jets");
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        outputJets.Clear();
        AppendFrom(aKnob);
        AppendFrom(bKnob);
        AppendFrom(cKnob);
        AppendFrom(dKnob);
        jetsOutputKnob.SetValue(outputJets.ToArray());
        return true;
    }

    private void AppendFrom(ValueConnectionKnob knob)
    {
        if (!knob.connected())
            return;
        Jet[] jets = knob.GetValue<Jet[]>();
        if (jets != null)
            outputJets.AddRange(jets);
    }
}
