using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Canopy))]
public class CanopyEditor : Editor {
    public override void OnInspectorGUI()
    {
        Canopy canopy = target as Canopy;
        if (DrawDefaultInspector())
        {
            //Something changed!
        }
        if (GUILayout.Button("Generate Strips"))
        {
            canopy.GenerateStrips();
        }
        if (GUILayout.Button("Clear Strips"))
        {
            canopy.ClearStrips();
        }
        if (GUILayout.Button("Rotate pixel base"))
        {
            canopy.RotatePixelBase();
        }
    }
}
