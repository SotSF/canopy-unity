using UnityEngine;
using System.Collections;

/* A controller pattern takes thumbstick input from a controller
 */
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
