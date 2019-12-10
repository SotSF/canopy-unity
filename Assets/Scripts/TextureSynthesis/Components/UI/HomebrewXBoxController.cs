using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomebrewXBoxController : MonoBehaviour {

    static HomebrewXBoxController _instance;
    public static HomebrewXBoxController instance
    {
        get
        {
            return _instance;
        }
    }

    enum Axis:int
    {
        Joyaxis0 = 0, Joyaxis1, Joyaxis2, Joyaxis3, Joyaxis4, Joyaxis5, Joyaxis6, Joyaxis7, Joyaxis8, Joyaxis9, Joyaxis10,
        Joybutton0 = 11, Joybutton1 = 12, Joybutton2 = 13, Joybutton3 = 14, Joybutton4 = 15,
        Joybutton5 = 16, Joybutton6 = 17, Joybutton7 = 18, Joybutton8 = 19, Joybutton9 = 20,
        Joybutton10 = 21, Joybutton11 = 22, Joybutton12 = 23,  Joybutton13 = 24,  Joybutton14 = 25,
        Joybutton15 = 26, Joybutton16 = 27, Joybutton17 = 28, Joybutton18 = 29, Joybutton19 = 30
    };

    public enum ControlInput : int
    {
        leftStickX, leftStickY, leftStickClick,
        rightStickX, rightStickY, rightStickClick,

        dpadX, dpadY,
        leftTrigger, rightTrigger,

        a, b, x, y,
        leftBumper, rightBumper,

        back, start
    }

    private Dictionary<ControlInput, Axis> controlAxes;
    private Dictionary<Axis, string> axisNames;
    private float[] controlValues = new float[30];

    private void Awake()
    {
        _instance = this;
        //controlValues = new Dictionary<ControlInput, float>();

        controlAxes = new Dictionary<ControlInput, Axis>();
        axisNames = new Dictionary<Axis, string>();
        foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            axisNames[axis] = axis.ToString();

        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            controlAxes[ControlInput.leftStickX] = Axis.Joyaxis1;
            controlAxes[ControlInput.leftStickY] = Axis.Joyaxis2;
            controlAxes[ControlInput.rightStickX] = Axis.Joyaxis4;
            controlAxes[ControlInput.rightStickY] = Axis.Joyaxis5;
            controlAxes[ControlInput.dpadX] = Axis.Joyaxis6;
            controlAxes[ControlInput.dpadY] = Axis.Joyaxis7;
            controlAxes[ControlInput.leftTrigger] = Axis.Joyaxis9;
            controlAxes[ControlInput.rightTrigger] = Axis.Joyaxis10;
            controlAxes[ControlInput.a] = Axis.Joybutton0;
            controlAxes[ControlInput.b] = Axis.Joybutton1;
            controlAxes[ControlInput.x] = Axis.Joybutton2;
            controlAxes[ControlInput.y] = Axis.Joybutton3;
            controlAxes[ControlInput.leftBumper] = Axis.Joybutton4;
            controlAxes[ControlInput.rightBumper] = Axis.Joybutton5;
            controlAxes[ControlInput.back] = Axis.Joybutton6;
            controlAxes[ControlInput.start] = Axis.Joybutton7;
            controlAxes[ControlInput.leftStickClick] = Axis.Joybutton8;
            controlAxes[ControlInput.leftStickClick] = Axis.Joybutton9;
        }
        else if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            controlAxes[ControlInput.leftStickX] = Axis.Joyaxis1;
            controlAxes[ControlInput.leftStickY] = Axis.Joyaxis2;
            controlAxes[ControlInput.rightStickX] = Axis.Joyaxis3;
            controlAxes[ControlInput.rightStickY] = Axis.Joyaxis4;
            // note: this will only work with left and up for mac for now.
            controlAxes[ControlInput.dpadX] = Axis.Joybutton7;
            controlAxes[ControlInput.dpadY] = Axis.Joybutton5;
            controlAxes[ControlInput.leftTrigger] = Axis.Joyaxis5;
            controlAxes[ControlInput.rightTrigger] = Axis.Joyaxis6;
            controlAxes[ControlInput.a] = Axis.Joybutton16;
            controlAxes[ControlInput.b] = Axis.Joybutton17;
            controlAxes[ControlInput.x] = Axis.Joybutton18;
            controlAxes[ControlInput.y] = Axis.Joybutton19;
            controlAxes[ControlInput.leftBumper] = Axis.Joybutton13;
            controlAxes[ControlInput.rightBumper] = Axis.Joybutton14;
            controlAxes[ControlInput.back] = Axis.Joybutton10;
            controlAxes[ControlInput.start] = Axis.Joybutton9;
            controlAxes[ControlInput.leftStickClick] = Axis.Joybutton11;
            controlAxes[ControlInput.leftStickClick] = Axis.Joybutton12;
        }
    }

    public float Get(ControlInput controlInput)
    {
        return controlValues[(int)controlInput];
    }
    
	// Update is called once per frame
	void Update () {
        foreach (ControlInput input in controlAxes.Keys) {
            controlValues[(int)input] = Input.GetAxis(axisNames[controlAxes[input]]);
        }
	}
}
