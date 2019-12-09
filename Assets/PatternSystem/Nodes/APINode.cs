using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;


[Node(false, "Inputs/API")]
public class ApiNode : Node
{
    public const string ID = "apiNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "API"; } }
    public override Vector2 DefaultSize { get { return new Vector2(200, 150); } }

    [ValueConnectionKnob("PollHZ", Direction.In, typeof(float))]
    public ValueConnectionKnob pollingInputKnob;

    [ValueConnectionKnob("Endpoint", Direction.In, typeof(string))]
    public ValueConnectionKnob endpointInputKnob;

    //[ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    //public ValueConnectionKnob textureOutputKnob;

    string endpoint;
    float pollhz;

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        pollingInputKnob.DisplayLayout(new GUIContent("PollHZ", "Number of times per second to poll the endpoint"));
        if (!pollingInputKnob.connected())
        {
            pollhz = RTEditorGUI.Slider(pollhz, -1, 1);
        }
        endpointInputKnob.DisplayLayout(new GUIContent("Endpoint", "API endpoint to poll"));
        if (!endpointInputKnob.connected())
        {
            endpoint = RTEditorGUI.TextField(endpoint);
        }
        GUILayout.EndVertical();
        //Dynamic JSON output here
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        string endpoint = endpointInputKnob.GetValue<string>();
        if (endpoint == null || endpoint == "")
        {
            // ResetOutputs()
            return true;
        }

        // Assign output channels
        

        return true;
    }
}