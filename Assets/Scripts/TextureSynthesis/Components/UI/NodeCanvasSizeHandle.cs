using UnityEngine;
using UnityEngine.EventSystems;
using NodeEditorFramework.Standard;

public class NodeCanvasSizeHandle : MonoBehaviour, IDragHandler
{
    public RTNodeEditor nodeCanvas;
    private Rect originalCanvasRect;
    private Rect originalRootRect;

    // Start is called before the first frame update
    void Start()
    {
        nodeCanvas.specifiedCanvasRect.width = Screen.width - 60;
        nodeCanvas.specifiedCanvasRect.height = Screen.height - 60;
        nodeCanvas.specifiedRootRect.width = Screen.width - 60;
        nodeCanvas.specifiedRootRect.height = Screen.height - 60;
        originalCanvasRect = nodeCanvas.specifiedCanvasRect;
        originalRootRect = nodeCanvas.specifiedRootRect;
        transform.position = new Vector3(originalCanvasRect.width+10, (Screen.height - originalCanvasRect.height)+40, 0);
    }

    public void OnDrag(PointerEventData data)
    {
        transform.position = new Vector3(data.position.x, data.position.y, 0);
        nodeCanvas.specifiedCanvasRect.width = data.position.x-10;
        nodeCanvas.specifiedCanvasRect.height = Screen.height - data.position.y;
        nodeCanvas.specifiedRootRect.width = data.position.x-10;
        nodeCanvas.specifiedRootRect.height = Screen.height - data.position.y;
    }
}
