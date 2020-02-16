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
        originalCanvasRect = nodeCanvas.specifiedCanvasRect;
        originalRootRect = nodeCanvas.specifiedRootRect;
        transform.position = new Vector3(originalCanvasRect.width, Screen.height - originalCanvasRect.height, 0);
    }

    public void OnDrag(PointerEventData data)
    {
        transform.position = new Vector3(data.position.x, data.position.y, 0);
        nodeCanvas.specifiedCanvasRect.width = data.position.x;
        nodeCanvas.specifiedCanvasRect.height = Screen.height - data.position.y;
        nodeCanvas.specifiedRootRect.width = data.position.x;
        nodeCanvas.specifiedRootRect.height = Screen.height - data.position.y;
    }
}
