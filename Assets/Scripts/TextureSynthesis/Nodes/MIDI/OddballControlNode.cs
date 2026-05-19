
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;


/* Very WIP, needs to have the code for the oddball CCs to be read and assigned to output ports.
 * 
 * */
[Node(false, "MIDI/OddballControl")]
public class OddballControlNode : TickingNode
{
    /* Oddball reference: https://docs.google.com/document/d/14L2wokwEkl3OIqeRpiXxA0IOLzT7xJSZx7kKNLflzVw/edit?tab=t.0
     * Everything is channel 1
     Notes:
       1 Tap / Bounce
       2 Shake
       3 Twist
     CCs:
       0 MoveSmoothed  
       1 Spin Smoothed
       2 Free Fall
       2 X orientation
       3 Y orientation
       4 Z orientation
       5 Energy
     */
    public override string GetID => "OddballControlNode";
    public override string Title { get { return "OddballControl"; } }


    private Vector2 _DefaultSize = new Vector2(150, 400);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    private int controlID;
    public int channel;
    private string nodeInstanceId;

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();

        // If already bound, register with MidiDeviceManager
        MidiDeviceManager.Instance.RegisterControlHandler(nodeInstanceId, channel, controlID, ReceiveMIDIMessage);
    }

    private void OnDestroy()
    {
        // Unregister from MidiDeviceManager
        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.UnregisterNode(nodeInstanceId);
        }
    }

    private void OnDisable()
    {
        OnDestroy();
    }


    void ReceiveMIDIMessage(Minis.MidiValueControl cc, float value)
    {
        if (cc.controlNumber == controlID)
        {
            var rawMIDIValue = value;
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        
        GUILayout.EndVertical();
        valueKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        var rawMIDIValue = 0f; 
        float val = rawMIDIValue;
        valueKnob.SetValue(val);
        return true;
    }
}
