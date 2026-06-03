
using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Linq;

using UnityEngine;

[Node(false, "Canopy/SimulationView")]
public class CanopySimulationNode : TextureSynthNode
{
    public override string GetID => "CanopySimulationView";
    public override string Title { get { return "CanopySimulationView"; } }
    private Vector2 _DefaultSize = new Vector2(1024, 1024);

    public override Vector2 DefaultSize => _DefaultSize;


    //private float speedFactor = 1;
    private RenderTexture camImage;

    private Camera cam;
    private GameObject sceneObj;

    public override void DoInit()
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
        // The box's rect is in node-local GUI space, same as Event.current.mousePosition inside
        // NodeGUI (the framework wraps the body in BeginGroup/BeginArea); Node.rect is canvas-space
        // and would never contain the local mouse position. We only report hover here - the actual
        // rotation grab is latched in MovementControls so it survives the cursor leaving the node.
        // Layout passes have no valid rects yet, so only sample on Repaint.
        Rect viewRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.Repaint && MovementControls.instance != null)
            MovementControls.instance.mouseOverView = viewRect.Contains(Event.current.mousePosition);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();


        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
}