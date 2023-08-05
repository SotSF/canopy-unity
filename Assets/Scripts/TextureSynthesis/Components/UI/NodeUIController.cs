using UnityEngine;
using UnityEditor;
using System.Collections;
using NodeEditorFramework.Standard;
using Lightsale.Utility;
using NodeEditorFramework;
using System.Collections.Generic;
using Lightsale.Animation;
using UnityEngine.UI;
using System;

public class NodeUIController : MonoBehaviour
{
    public static NodeUIController instance;
    public RTNodeEditor nodeCanvas;
    public float minimizeTime;
    public Color textureFlowColor;
    public Material canopyMaterial;
    public AnimationCurve easingCurve;

    [Tooltip("Objects which should only be displayed in simulator mode")]
    public Transform[] simulationOnlyObjects;

    public bool inSimulatorMode = true;

    private Rect originalCanvasRect;
    private Rect originalRootRect;
    private bool minimized = false;

    private Coroutine toggleVisibilityRoutine;
    private Coroutine animationRoutine;

    public Button viewModeButton;
    private Vector3 controllerCameraPosition = new Vector3(0.75f, 1.6f, 0);

    private void Awake()
    {
        instance = this;
        List<string> styleIDs = new List<string>();
        Invoke("EnterControllerView", 0.1f);
        //var x = ConnectionPortStyles.GetPortStyle("UnityEngine.Texture");
        //x.SetColor(textureFlowColor);
    }

    private void Start()
    {
        originalCanvasRect = nodeCanvas.specifiedCanvasRect;
        originalRootRect = nodeCanvas.specifiedRootRect;
        nodeCanvas.specifiedCanvasRect.width = Screen.width;
        nodeCanvas.specifiedCanvasRect.height = Screen.height;
        nodeCanvas.specifiedRootRect.width = Screen.width;
        nodeCanvas.specifiedRootRect.height = Screen.height;
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

    //Viewing mode controls
    public void EnterSimulatorView()
    {
        inSimulatorMode = true;
        foreach (Transform obj in simulationOnlyObjects)
        {
            obj.gameObject.SetActive(true);
        }
        viewModeButton.GetComponentInChildren<Text>().text = "Enter controller view";
        Canopy.instance.EnterSimulationMode();
    }

    public void EnterControllerView()
    {
        inSimulatorMode = false;
        foreach (Transform obj in simulationOnlyObjects)
        {
            obj.gameObject.SetActive(false);
        }
        //viewModeButton.GetComponentInChildren<Text>().text = "Enter simulator view";
        Canopy.instance.EnterControllerMode();
        var trans = Animations.LocalPositionLerp(Camera.main.transform, controllerCameraPosition);
        var rotate = Animations.LocalQuatLerp(Camera.main.transform, Quaternion.identity);
        this.CheckedRoutine(ref animationRoutine, Animations.CubicTimedAnimator(1.2f, trans, rotate));
    }

    public void ToggleSimulatorView()
    {
        if (inSimulatorMode)
            EnterControllerView();
        else
            EnterSimulatorView();
    }
}