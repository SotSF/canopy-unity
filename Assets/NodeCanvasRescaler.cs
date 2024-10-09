using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using NodeEditorFramework.Standard;
using UnityEngine;

public class NodeCanvasRescaler : UIBehaviour
{
    public RTNodeEditor nodeCanvas;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        nodeCanvas.specifiedCanvasRect.width = Screen.width;
        nodeCanvas.specifiedCanvasRect.height = Screen.height;
        nodeCanvas.specifiedRootRect.width = Screen.width;
        nodeCanvas.specifiedRootRect.height = Screen.height;
    }
}