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

    private static Dictionary<float, GUILayoutOption> widths = new();
    private static Dictionary<float, GUILayoutOption> heights = new();

    public static void TexInfo(Texture tex, float width, float height)
    {
        GUILayout.BeginVertical();
        if (!widths.ContainsKey(width))
            widths[width] = GUILayout.MaxWidth(width);
        if (!heights.ContainsKey(height))
            heights[height] = GUILayout.MaxHeight(height);
        GUILayout.Box(tex, widths[width], heights[height]);
        GUILayout.Label("'" + TrimName(tex.name) + "'");
        GUILayout.Label(tex.width + "x" + tex.height);
        GUILayout.EndVertical();
    }
}
