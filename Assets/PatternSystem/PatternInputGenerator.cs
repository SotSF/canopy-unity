using UnityEngine;
using System.Collections;
using System;

public class PatternInputGenerator : MonoBehaviour
{
    static PatternInputGenerator _instance;
    public static PatternInputGenerator instance { get { return _instance; } }

    private void Awake()
    {
        _instance = this;
    }

    #region inputGenerators

    public static float TimeSeconds()
    {
        return Time.time;
    }

    public static float ManagerPeriod()
    {
        return PatternManager.instance.period;
    }

    public static float ManagerCycleCount()
    {
        return PatternManager.instance.cycles;
    }

    public static float ManagerBrightness()
    {
        return PatternManager.instance.brightness + PatternManager.instance.brightnessMod;
    }

    public static Func<float> XboxInput(XboxController.ControlInput controlInput)
    {
        Func<float> getValue = () =>
        {
            //Debug.Log(XboxController.instance.Get(controlInput));
            return XboxController.instance.Get(controlInput);
        };
        return getValue;
    }

    #endregion
}