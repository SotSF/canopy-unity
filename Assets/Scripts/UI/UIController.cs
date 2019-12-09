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
    public RectTransform textureBasePrefab;

    [Tooltip("Objects which should only be displayed in simulator mode")]
    public Transform[] simulationOnlyObjects;

    //public UIControl controlBase;
    public RectTransform controlsNode;

    [HideInInspector]
    public bool inSimulatorMode = true;
    private bool inHighPerformanceMode = false;

    public bool sendToAPI { get { return sendToAPIToggle.isOn; } }

    private Button viewModeButton;
    private Light canopyLight;

    private Vector3 controllerCameraPosition = new Vector3(0.75f, 1.6f, 0);
    private Coroutine animationRoutine;

    private Toggle sendToAPIToggle;
    

    private void Awake()
    {
        instance = this;
        viewModeButton = transform.Find("ControlButtons/ViewModeButton").GetComponent<Button>();
        canopyLight = Canopy.instance.GetComponentInChildren<Light>();
        sendToAPIToggle = transform.Find("ControlButtons/SendToCanopyToggle").GetComponentInChildren<Toggle>();
        Invoke("EnterControllerView", 0.5f);
    }


    // Update the displayed UI controls (sliders, etc) to match a given Pattern's
    // input parameters
    public void UpdateUIControls(Pattern pattern)
    {
        //Rect position = new Rect();
        var controls = controlsNode.GetComponentsInChildren<UIControl>();
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
            if (param.input) {
                RectTransform controlBase = Instantiate(controlBasePrefab, controlsNode);
                UIControl control = controlBase.GetComponent<UIControl>();
                Text label = controlBase.Find("ControlElements/Label").GetComponent<Text>();
                RectTransform anchor = controlBase.Find("ControlElements/ControlAnchor").GetComponent<RectTransform>();
                label.text = param.name;
                control.attachParameter(param, anchor);
                controlBase.SetParent(controlsNode, false);
                controlBase.anchoredPosition = anchored;
                if (param.paramType == ParamType.FLOAT4)
                {
                    rows += 4;
                } else
                {
                    rows++;
                }
                anchored -= new Vector2(0, rowHeight);
            }
        }
        // if AnimatedPattern, show 'next' button
        // ugly hardcoded hack but w/e
        if (pattern.GetType() == typeof(AnimatedPattern))
        {
            var animPattern = pattern as AnimatedPattern;
            var nextButton = Instantiate(animPattern.nextButton, controlsNode).GetComponent<Button>();
            nextButton.onClick.AddListener(animPattern.Next);
        }
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
        viewModeButton.GetComponentInChildren<Text>().text = "Enter simulator view";
        Canopy.instance.EnterControllerMode();
        var trans = Animations.LocalPositionLerp(Camera.main.transform, controllerCameraPosition);
        var rotate = Animations.LocalQuatLerp(Camera.main.transform, Quaternion.identity);
        this.CheckedRoutine(ref animationRoutine, Animations.CubicTimedAnimator(1.2f, trans, rotate));
    }
    public void ToggleSimulatorView()
    {
        if (inSimulatorMode)
        {
            EnterControllerView();
        } else
        {
            EnterSimulatorView();
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