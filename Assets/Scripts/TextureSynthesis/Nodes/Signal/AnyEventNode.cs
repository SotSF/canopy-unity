using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Logical OR over a dynamically expandable list of bool/event inputs: outputs true while
/// any input is true. Follows the SignalMux pattern of always keeping one open input slot,
/// so wiring an event in grows the list. Useful for funneling several timeline events into
/// one control signal (e.g. a TextureMux cycle trigger).
/// </summary>
[Node(false, "Signal/Any")]
public class AnyEventNode : TextureSynthNode
{
    public override string GetID => "AnyEventNode";
    public override string Title { get { return "Any"; } }

    private Vector2 _DefaultSize = new Vector2(130, 80);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(130, 0);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("any", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob outputKnob;

    private bool lastOutput;

    private int connectedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < dynamicConnectionPorts.Count; i++)
                if (dynamicConnectionPorts[i].connected()) n++;
            return n;
        }
    }

    private int targetPortCount => connectedCount + 1;

    // Keep exactly one open slot at the bottom of the input list
    private void SetPortCount()
    {
        if (dynamicConnectionPorts.Count > targetPortCount)
        {
            for (int i = dynamicConnectionPorts.Count - 1; i >= 0 && dynamicConnectionPorts.Count > targetPortCount; i--)
            {
                if (!dynamicConnectionPorts[i].connected())
                {
                    DeleteConnectionPort(i);
                }
            }
        }
        else if (dynamicConnectionPorts.Count < targetPortCount)
        {
            var attrib = new ValueConnectionKnobAttribute("event", Direction.In, typeof(bool), NodeSide.Left);
            while (dynamicConnectionPorts.Count < targetPortCount)
                CreateValueConnectionKnob(attrib);
        }
        _DefaultSize = new Vector2(130, 44 + targetPortCount * 24);
    }

    public override void NodeGUI()
    {
        SetPortCount();

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            if (port.connected())
            {
                GUILayout.Label(port.GetValue<bool>() ? "●" : "○", GUILayout.Width(18));
            }
            else
            {
                GUILayout.Label("+ event", GUILayout.ExpandWidth(false));
            }
            port.SetPosition();
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label(lastOutput ? "any: ●" : "any: ○", GUILayout.Width(48));
        outputKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        bool any = false;
        for (int i = 0; i < dynamicConnectionPorts.Count && !any; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            if (port.connected() && port.GetValue<bool>()) any = true;
        }
        lastOutput = any;
        if (outputKnob != null) outputKnob.SetValue(any);
        return true;
    }
}
