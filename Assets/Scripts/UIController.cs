using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Button viewModeButton;
    [Tooltip("Objects which should only be displayed in simulator mode")]
    public Transform[] simulationOnlyObjects;

    private bool inSimulatorMode = true;



    public void SetHighPerformanceMode()
    {

    }

    //Viewing mode controls
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
    public void EnterControllerView()
    {
        foreach (Transform obj in simulationOnlyObjects)
        {
            obj.gameObject.SetActive(false);
        }
        viewModeButton.GetComponentInChildren<Text>().text = "Enter simulator view";
    }

    public void EnterSimulatorView()
    {
        foreach (Transform obj in simulationOnlyObjects)
        {
            obj.gameObject.SetActive(true);
        }
        viewModeButton.GetComponentInChildren<Text>().text = "Enter controller view";
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