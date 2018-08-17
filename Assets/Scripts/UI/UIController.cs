using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Lightsale.Animation;
using Lightsale.Utility;
using sotsf.canopy.patterns;
using System.Collections.Generic;

public class UIController : MonoBehaviour
{
    public static UIController instance;

    public RectTransform controlBasePrefab;
    public RectTransform inputBasePrefab;
    public RectTransform sliderBasePrefab;
    public RectTransform toggleBasePrefab;

    [Tooltip("Objects which should only be displayed in simulator mode")]
    public Transform[] simulationOnlyObjects;

    public Dictionary<string, UIControl> uiControlMap;

    //public UIControl controlBase;
    public RectTransform controlsNode;

    [HideInInspector]
    public bool inSimulatorMode = true;
    private bool inHighPerformanceMode = false;

    public bool sendToAPI { get { return sendToAPIToggle.isOn; } }

    private Button viewModeButton;
    private Button performanceModeButton;
    private Light canopyLight;

    private Vector3 controllerCameraPosition = new Vector3(0, 1.6f, 0);
    private Coroutine animationRoutine;

    private Toggle sendToAPIToggle;


    private void Awake()
    {
        instance = this;
        uiControlMap = new Dictionary<string, UIControl>();
        viewModeButton = transform.Find("ControlButtons/ViewModeButton").GetComponent<Button>();
        performanceModeButton = transform.Find("ControlButtons/PerformanceModeButton").GetComponent<Button>();
        canopyLight = Canopy.instance.GetComponentInChildren<Light>();
        sendToAPIToggle = transform.Find("ControlButtons/SendToCanopyToggle").GetComponentInChildren<Toggle>();
    }

    //Performance mode controls (only render single active pattern)
    public void EnterHighPerformanceMode()
    {
        PatternManager.instance.highPerformance = true;
        canopyLight.enabled = false;
        performanceModeButton.GetComponentInChildren<Text>().text = "Enter high quality mode";
    }
    public void EnterHighQualityMode()
    {
        PatternManager.instance.highPerformance = false;
        canopyLight.enabled = true;
        performanceModeButton.GetComponentInChildren<Text>().text = "Enter high performance mode";
    }
    public void TogglePerformanceMode()
    {
        inHighPerformanceMode = !inHighPerformanceMode;
        if (inHighPerformanceMode)
        {
            EnterHighPerformanceMode();
        } else
        {
            EnterHighQualityMode();
        }
    }


    // Update the displayed UI controls (sliders, etc) to match a given Pattern's
    // input parameters
    public void UpdateUIControls(Pattern pattern)
    {
        //Rect position = new Rect();
        var controls = controlsNode.GetComponentsInChildren<UIControl>();
        uiControlMap = new Dictionary<string, UIControl>();
        // Destroy old controls

        Vector2 anchored = new Vector2(0, 0);
        const int rowHeight = 30;
        int rows = 0;
        foreach (UIControl control in controls)
        {
            Destroy(control.gameObject);
        }
        foreach (PatternParameter param in pattern.parameters)
        {
            if (param.controllable) {
                RectTransform controlBase = Instantiate(controlBasePrefab, controlsNode);
                UIControl control = controlBase.GetComponent<UIControl>();
                Text label = controlBase.Find("ControlElements/Label").GetComponent<Text>();
                RectTransform anchor = controlBase.Find("ControlElements/ControlAnchor").GetComponent<RectTransform>();
                label.text = param.name;
                control.attachParameter(param, anchor);
                controlBase.SetParent(controlsNode, false);
                controlBase.anchoredPosition = anchored;
                rows++;
                anchored -= new Vector2(0, rowHeight);

                uiControlMap[param.name] = control;
            }
        }
    }

    //Viewing mode controls
    public void EnterSimulatorView()
    {
        foreach (Transform obj in simulationOnlyObjects)
        {
            obj.gameObject.SetActive(true);
        }
        viewModeButton.GetComponentInChildren<Text>().text = "Enter controller view";
        Canopy.instance.EnterSimulationMode();
    }
    public void EnterControllerView()
    {
        foreach (Transform obj in simulationOnlyObjects)
        {
            obj.gameObject.SetActive(false);
        }
        viewModeButton.GetComponentInChildren<Text>().text = "Enter simulator view";
        Canopy.instance.EnterControllerMode();
        var trans = Animations.LocalPositionLerp(Camera.main.transform, controllerCameraPosition);
        var rotate = Animations.LocalQuatLerp(Camera.main.transform, Quaternion.identity);
        this.CheckedRoutine(ref animationRoutine, Animations.CubicTimedAnimator(1.2f, trans, rotate));
    }
    public void ToggleSimulatorView()
    {
        inSimulatorMode = !inSimulatorMode;
        if (inSimulatorMode)
        {
            EnterSimulatorView();
        } else
        {
            EnterControllerView();
        }
    }


    public void BrightnessUpdate(float brightness)
    {
        PatternManager.instance.brightness = brightness;
    }
    public void HueUpdate(float hue)
    {
        PatternManager.instance.hue = hue;
    }
    public void SaturationUpdate(float saturation)
    {
        PatternManager.instance.saturation = saturation;
    }
    public void PeriodUpdate(float period)
    {
        PatternManager.instance.period = period;
    }
    public void CyclesUpdate(float cycles)
    {
        PatternManager.instance.cycles = cycles;
    }
}