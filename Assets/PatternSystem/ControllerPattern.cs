using UnityEngine;
using System.Collections;
using System;

namespace sotsf.canopy.patterns
{
    public class ControllerPattern : Pattern
    {
        public Func<float> rightStickX = PatternInputGenerator.XboxInput(XboxController.ControlInput.rightStickX);
        public Func<float> rightStickY = PatternInputGenerator.XboxInput(XboxController.ControlInput.rightStickY);
        public Func<float> leftStickX = PatternInputGenerator.XboxInput(XboxController.ControlInput.leftStickX);
        public Func<float> leftStickY = PatternInputGenerator.XboxInput(XboxController.ControlInput.leftStickY);


        protected override void UpdateRenderParams()
        {
            base.UpdateRenderParams();
            renderParams["rightStickX"] = rightStickX();
            renderParams["rightStickY"] = rightStickY();
            renderParams["leftStickX"] = leftStickX();
            renderParams["leftStickY"] = leftStickY();
        }
    }
}