using System;
using System.Collections;
using System.Collections.Generic;
using sotsf.canopy.patterns;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIControl : MonoBehaviour
{
    RectTransform control;
    public void attachParameter(PatternParameter param, RectTransform parent)
    {
        switch (param.paramType)
        {
            case (ParamType.FLOAT):
            case (ParamType.INT):
                var prefab = param.useRange ? UIController.instance.sliderBasePrefab : UIController.instance.inputBasePrefab;
                control = Instantiate(prefab, parent);
                if (param.useRange){
                    var slider = control.GetComponentInChildren<Slider>();
                    if (param.paramType == ParamType.INT){
                        slider.minValue = param.minInt;
                        slider.maxValue = param.maxInt;
                        slider.value = param.defaultInt;
                        slider.wholeNumbers = true;
                    } else {
                        slider.minValue = param.minFloat;
                        slider.maxValue = param.maxFloat;
                        slider.value = param.defaultFloat;
                        slider.wholeNumbers = false;
                    }
                }
                break;
            case (ParamType.BOOL):
                control = Instantiate(UIController.instance.toggleBasePrefab, parent);
                break;
            case (ParamType.TEXTURE):
                //Display texture input?
                break;
        }
    }

    public float getFloat()
    {
        Slider slider = control.GetComponentInChildren<Slider>();
        return slider.value;
    }

    public bool getBool()
    {
        Toggle toggle = control.GetComponentInChildren<Toggle>();
        return toggle.isOn;
    }
}