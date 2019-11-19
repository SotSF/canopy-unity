using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NodeEditorFramework.TextureComposer;

public static class NodeUIElements
{
    const int MAX_NAME_LENGTH = 12;
    public static string TrimName(string name)
    {
        if (name.Length < MAX_NAME_LENGTH)
        {
            return name;
        } else
        {
            return name.Substring(0, 8) + "...";
        }
    }

    public static void TexInfo(Texture tex, float width=0, float height=0, bool showAttribs=true)
    {
        GUILayout.BeginVertical();
        var layoutParams = new List<GUILayoutOption>();
        if (width > 0)
            layoutParams.Add(GUILayout.MaxWidth(width));
        if (height > 0)
            layoutParams.Add(GUILayout.MaxHeight(height));
        GUILayout.Box(tex, layoutParams.ToArray());
        GUILayout.Label("'" + TrimName(tex.name) + "'");
        GUILayout.Label(tex.width + "x" + tex.height);
        GUILayout.EndVertical();
    }
}
