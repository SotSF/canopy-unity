using UnityEngine;
using UnityEditor;
using System.Collections;
using NodeEditorFramework.Standard;
using Lightsale.Utility;

public class NodeUIController : MonoBehaviour
{
    public static NodeUIController instance;
    public RTNodeEditor nodeCanvas;
    public float minimizeTime;
    public Material canopyMaterial;
    public AnimationCurve easingCurve;

    Rect originalCanvasRect;
    Rect originalRootRect;
    bool minimized = false;
    private Coroutine toggleVisibilityRoutine;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        originalCanvasRect = nodeCanvas.specifiedCanvasRect;
        originalRootRect = nodeCanvas.specifiedRootRect;
    }

    IEnumerator MinimizeRoutine()
    {
        float start = Time.time;
        while (Time.time < start + minimizeTime)
        {
            var height = (1 - easingCurve.Evaluate((Time.time - start) / minimizeTime)) * originalCanvasRect.height;
            nodeCanvas.specifiedCanvasRect.height = height;
            nodeCanvas.specifiedRootRect.height = height+22;
            yield return null;
        }
        nodeCanvas.specifiedCanvasRect.height = 0;
        nodeCanvas.specifiedRootRect.height = 22;
    }

    IEnumerator MaximizeRoutine()
    {
        float start = Time.time;
        while (Time.time < start + minimizeTime)
        {
            var height = easingCurve.Evaluate((Time.time - start) / minimizeTime) * originalCanvasRect.height;
            nodeCanvas.specifiedCanvasRect.height = height;
            nodeCanvas.specifiedRootRect.height = height+22;
            yield return null;
        }
        nodeCanvas.specifiedCanvasRect.height = originalCanvasRect.height;
        nodeCanvas.specifiedRootRect.height = originalCanvasRect.height+22;
    }

    public void ToggleCanvasVisibility()
    {
        if (!minimized)
        {
            this.CheckedRoutine(ref toggleVisibilityRoutine, MinimizeRoutine());
        } else
        {
            this.CheckedRoutine(ref toggleVisibilityRoutine, MaximizeRoutine());
        }
        minimized = !minimized;
    }
}