using System;
using System.Collections;
using System.Collections.Generic;
using sotsf.canopy.patterns;
using UnityEngine;
using UnityEngine.UI;

public class UIControl : MonoBehaviour
{
    public void attachParameter(PatternParameter param)
    {
        RectTransform control;
        switch (param.paramType)
        {
            case (ParamType.FLOAT):
                if (param.useRange)
                {
                    control = Instantiate(UIController.instance.sliderBase);
                } else
                {
                    control = Instantiate(UIController.instance.inputBase);
                }
                break;
            case (ParamType.INT):
                if (param.useRange)
                {
                    control = Instantiate(UIController.instance.sliderBase);
                } else
                {
                    control = Instantiate(UIController.instance.inputBase);
                }
                break;
            case (ParamType.BOOL):
                control = Instantiate(UIController.instance.toggleBase);
                break;
            case (ParamType.TEXTURE):
                //Display texture input?
                break;
        }
    }
}