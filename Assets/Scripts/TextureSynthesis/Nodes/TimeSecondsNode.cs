
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Signal/TimeSeconds")]
public class TimeSecondsNode : TickingNode
{
    public override string GetID => "TimeSecondsNode";
    public override string Title { get { return "TimeSeconds"; } }

    public override Vector2 DefaultSize { get { return new Vector2(120, 80); } }

    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;

    private float outputSignal;

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Value: {0:0.00}", outputSignal));
        outputSignalKnob.DisplayLayout();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        outputSignal = Time.time;
        outputSignalKnob.SetValue(outputSignal);
        return true;
    }
}
