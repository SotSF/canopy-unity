using UnityEngine;
using System.Collections;

public class ControllerPattern : Pattern
{
    protected override void UpdateRenderParams()
    {
        base.UpdateRenderParams();
        renderParams["rightStickX"] = Input.GetAxis("RightStickX");
        renderParams["rightStickY"] = Input.GetAxis("RightStickY");
        renderParams["leftStickX"] = Input.GetAxis("LeftStickX");
        renderParams["leftStickY"] = Input.GetAxis("LeftStickY");
    }
}
