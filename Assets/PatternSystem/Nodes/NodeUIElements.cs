using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class NodeUIElements
{
    public static void TexInfo(Texture tex, float width=0, float height=0, bool showAttribs=true)
    {
        GUILayout.BeginVertical();
        var layoutParams = new List<GUILayoutOption>();
        if (width > 0)
            layoutParams.Add(GUILayout.MaxWidth(width));
        if (height > 0)
            layoutParams.Add(GUILayout.MaxHeight(height));
        GUILayout.Box(tex, layoutParams.ToArray());
        GUILayout.Label("'" + tex.name + "'");
        GUILayout.Label("Size: " + tex.width + "x" + tex.height + "");
        GUILayout.EndVertical();
    }
}
