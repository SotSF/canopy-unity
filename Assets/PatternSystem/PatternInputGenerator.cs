using UnityEngine;
using System.Collections;
using System;

public class PatternInputGenerator : MonoBehaviour
{
    public static PatternInputGenerator instance { get; private set; }

    private void Awake()
    {
        instance = this;
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
            if (XboxController.instance != null)
            {
                return XboxController.instance.Get(controlInput);
            } else
            {
                return 0;
            }

        };
        return getValue;
    }

    #endregion
}