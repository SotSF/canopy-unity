using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class XboxController : MonoBehaviour {

    enum Axis:int
    {
        Joyaxis0 = 0, Joyaxis1, Joyaxis2, Joyaxis3, Joyaxis4, Joyaxis5, Joyaxis6, Joyaxis7, Joyaxis8, Joyaxis9, Joyaxis10,
        Joybutton0 = 11, Joybutton1 = 12, Joybutton2 = 13, Joybutton3 = 14, Joybutton4 = 15,
        Joybutton5 = 16, Joybutton6 = 17, Joybutton7 = 18, Joybutton8 = 19, Joybutton9 = 20,
        Joybutton10 = 21, Joybutton11 = 22, Joybutton12 = 23,  Joybutton13 = 24,  Joybutton14 = 25,
        Joybutton15 = 26, Joybutton16 = 27, Joybutton17 = 28, Joybutton18 = 29, Joybutton19 = 30
    };

    float[] controls = new float[30];

    Axis leftStickX, leftStickY, leftStickClick;
    Axis rightStickX, rightStickY, rightStickClick;

    Axis dpadX, dpadY;
    Axis leftTrigger, rightTrigger;

    Axis a, b, x, y;
    Axis leftBumper, rightBumper;

    Axis back, start;

    private Dictionary<Axis, string> axisNames;

    private void Awake()
    {
        axisNames = new Dictionary<Axis, string>();
        foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            axisNames[axis] = axis.ToString();

        Debug.Log(axisNames[Axis.Joyaxis0]);

        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            leftStickX = Axis.Joyaxis0;
            leftStickY = Axis.Joyaxis1;
            rightStickX = Axis.Joyaxis4;
            rightStickY = Axis.Joyaxis5;
            dpadX = Axis.Joyaxis6;
            dpadY = Axis.Joyaxis7;
            leftTrigger = Axis.Joyaxis9;
            rightTrigger = Axis.Joyaxis10;
            a = Axis.Joybutton0;
            b = Axis.Joybutton1;
            x = Axis.Joybutton2;
            y = Axis.Joybutton3;
            leftBumper = Axis.Joybutton4;
            rightBumper = Axis.Joybutton5;
            back = Axis.Joybutton6;
            start = Axis.Joybutton7;
            leftStickClick = Axis.Joybutton8;
            leftStickClick = Axis.Joybutton9;
        }
        else if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            leftStickX = Axis.Joyaxis0;
            leftStickY = Axis.Joyaxis1;
            rightStickX = Axis.Joyaxis3;
            rightStickY = Axis.Joyaxis4;
            // note: this will only work with left and up for mac for now.
            dpadX = Axis.Joybutton7;
            dpadY = Axis.Joybutton5;
            leftTrigger = Axis.Joyaxis5;
            rightTrigger = Axis.Joyaxis6;
            a = Axis.Joybutton16;
            b = Axis.Joybutton17;
            x = Axis.Joybutton18;
            y = Axis.Joybutton19;
            leftBumper = Axis.Joybutton13;
            rightBumper = Axis.Joybutton14;
            back = Axis.Joybutton10;
            start = Axis.Joybutton9;
            leftStickClick = Axis.Joybutton11;
            leftStickClick = Axis.Joybutton12;
        }
    }

    float Get(Axis axis)
    {
        return controls[(int)axis];
    }
    
	// Update is called once per frame
	void Update () {
		foreach (KeyValuePair<Axis, string> entry in axisNames)
        {
            controls[(int)entry.Key] = Input.GetAxis(entry.Value);
        }
	}
}
