using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Lightsale.Animation;
using Lightsale.Utility;

public class UIController : MonoBehaviour
{
    public static UIController instance;

    [Tooltip("Objects which should only be displayed in simulator mode")]
    public Transform[] simulationOnlyObjects;

    [HideInInspector]
    public bool inSimulatorMode = true;
    private bool inHighPerformanceMode = false;

    private Button viewModeButton;
    private Button performanceModeButton;
    private Light canopyLight;

    private Vector3 controllerCameraPosition = new Vector3(0, 1.6f, 0);
    private Coroutine animationRoutine;


    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        viewModeButton = transform.Find("ControlButtons/ViewModeButton").GetComponent<Button>();
        performanceModeButton = transform.Find("ControlButtons/PerformanceModeButton").GetComponent<Button>();
        canopyLight = Canopy.instance.GetComponentInChildren<Light>();
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