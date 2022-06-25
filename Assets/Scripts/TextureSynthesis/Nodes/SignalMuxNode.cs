
using Boo.Lang;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.ComponentModel.Design;
using System.Linq;
using UnityEngine;

[Node(false, "Signal/SignalMux")]
public class SignalMuxNode : Node
{
    public override string GetID => "SignalMuxNode";
    public override string Title { get { return "SignalMux"; } }

    public override Vector2 DefaultSize => new Vector2(180, (1+targetPortCount) * 100);
    public override Vector2 MinSize => new Vector2(180, 0);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("control", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob controlKnob;
    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;

    private float outputSignal;
    public int activeSignalIndex = 0;
    private int targetPortCount => activePortCount +1;
    private int activePortCount => dynamicConnectionPorts.Where(port => port.connected()).Count();
    private int openPortIndex => activePortCount;

    private void SetPortCount()
    {
        // Keep one open slot at the bottom of the input list
        // Adjust the active signal index if necessary
        if (dynamicConnectionPorts.Count > targetPortCount)
        { 
            for (int i = 0; i < dynamicConnectionPorts.Count-1; i++)
            {
                var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
                if (!port.connected())
                {
                    DeleteConnectionPort(i);
                    if (activeSignalIndex > i)
                        activeSignalIndex--;
                    else if (activeSignalIndex == i)
                        activeSignalIndex = 0;
                }
            }   
        } else if (dynamicConnectionPorts.Count < targetPortCount)
        {
            ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute("Add input", Direction.In, typeof(float));
            while (dynamicConnectionPorts.Count < targetPortCount)
                CreateValueConnectionKnob(outKnobAttribs);
        }
    }

    public override void NodeGUI()
    {
        SetPortCount();

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        controlKnob.DisplayLayout();
        for (int i = 0; i < targetPortCount-1; i++)
        {
            GUILayout.BeginHorizontal();
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            GUILayout.Space(4);
            GUILayout.Label(string.Format("{0:0.00}", port.GetValue<float>()));
            port.SetPosition();
            if (i == activeSignalIndex)
            {
                GUILayout.Label("[ Active ]");
            }
            else
            {
                if (GUILayout.Button("Activate", GUILayout.ExpandWidth(false)))
                {
                    activeSignalIndex = i;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        ((ValueConnectionKnob)dynamicConnectionPorts[openPortIndex]).DisplayLayout();
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        if (activePortCount > 0)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[activeSignalIndex];
            GUILayout.Label(string.Format("Output: {0:0.00}", port.GetValue<float>()), GUILayout.Width(60));
            GUILayout.Space(4);
        }
        outputSignalKnob.SetPosition();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    
    public override bool Calculate()
    {
        if (controlKnob.GetValue<bool>())
        {
            activeSignalIndex = (activeSignalIndex + 1) % activePortCount;
        }
        if (targetPortCount > 1)
        {
            var activePort = (ValueConnectionKnob)dynamicConnectionPorts[activeSignalIndex];
            outputSignalKnob.SetValue(activePort.GetValue<float>());
        }
        return true;
    }
}
