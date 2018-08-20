using System;
using System.Collections;
using System.Collections.Generic;
using sotsf.canopy.patterns;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIControl : MonoBehaviour
{
    PatternParameter param;
    RectTransform control;
    Text sliderLabel;
    TextureSelector texSelector;
    Slider slider;
    InputField input;
    Toggle toggle;

    public void attachParameter(PatternParameter param, RectTransform parent)
    {
        this.param = param;
        switch (param.paramType)
        {
            case (ParamType.FLOAT):
            case (ParamType.INT):
                var prefab = param.useRange ? UIController.instance.sliderBasePrefab : UIController.instance.inputBasePrefab;
                control = Instantiate(prefab, parent);
                if (param.useRange){
                    sliderLabel = control.Find("ValueDisplay").GetComponent<Text>();
                    slider = control.GetComponentInChildren<Slider>();
                    slider.onValueChanged.AddListener(UpdateSliderLabel);
                    if (param.paramType == ParamType.INT){
                        slider.minValue = param.minInt;
                        slider.maxValue = param.maxInt;
                        slider.value = param.defaultInt;
                        slider.wholeNumbers = true;
                    } else {
                        slider.minValue = param.minFloat;
                        slider.maxValue = param.maxFloat;
                        slider.value = param.GetFloat();
                        slider.wholeNumbers = false;
                    }
                    slider.onValueChanged.AddListener(param.SetFloat);
                } else
                {
                    input = control.GetComponentInChildren<InputField>();
                    input.onValueChanged.AddListener(SetFloat);
                }
                break;
            case (ParamType.BOOL):
                control = Instantiate(UIController.instance.toggleBasePrefab, parent);
                toggle = control.GetComponentInChildren<Toggle>();
                toggle.onValueChanged.AddListener(param.SetBool);
                break;
            case (ParamType.TEXTURE):
                control = Instantiate(UIController.instance.textureBasePrefab, parent);
                texSelector = control.GetComponentInChildren<TextureSelector>();
                texSelector.textureSelected.AddListener(SetTexture);
                texSelector.SelectTexture(param.GetTexture());
                break;
        }
    }

    public void SetTexture()
    {
        param.SetTexture(texSelector.tex);
    }

    public void SetFloat(string value)
    {
        param.SetFloat(float.Parse(value));
    }

    public void UpdateSliderLabel(float val)
    {
        sliderLabel.text = String.Format("{0:F2}", val);
    }
}