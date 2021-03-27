using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FullscreenOutput))]
public class FullscreenOutputEditor : Editor {
    public override void OnInspectorGUI()
    {
        FullscreenOutput output = target as FullscreenOutput;
        if (DrawDefaultInspector())
        {
            //Something changed!
        }
        if (GUILayout.Button("Reset"))
        {
            output.ClearTexture();
        }
    }
}
