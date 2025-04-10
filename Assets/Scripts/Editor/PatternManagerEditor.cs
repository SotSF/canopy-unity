﻿using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PatternManager))]
public class PatternManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PatternManager manager = target as PatternManager;
        if (DrawDefaultInspector())
        {
            //Something changed!
        }
        if (Application.isPlaying) { 
            if (GUILayout.Button("Next Pattern"))
            {
                manager.NextPattern();
            }
        } else
        {
            if (GUILayout.Button("Create new pattern"))
            {
                manager.CreateNewPattern();
            }
            if (GUILayout.Button("Arrange displays"))
            {
                manager.ArrangePatternDisplays();
            }
        }
    }
}
