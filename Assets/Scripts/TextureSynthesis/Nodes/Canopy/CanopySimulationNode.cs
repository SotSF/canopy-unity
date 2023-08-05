
using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Linq;

using UnityEngine;

[Node(false, "Canopy/SimulationView")]
public class CanopySimulationNode : TickingNode
{
    public override string GetID => "CanopySimulationView";
    public override string Title { get { return "CanopySimulationView"; } }

    public override Vector2 DefaultSize { get { return new Vector2(1024, 1024); } }


    //private float speedFactor = 1;
    private RenderTexture camImage;

    private Camera cam;
    private GameObject sceneObj;

    public void Awake()
    {
        sceneObj = GameObject.Find("CanopyCam");
        cam = sceneObj.GetComponent<Camera>();
        camImage = cam.targetTexture;
    }


    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(camImage, GUILayout.MaxWidth(1024), GUILayout.MaxHeight(1024));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();


        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        return true;
    }
}