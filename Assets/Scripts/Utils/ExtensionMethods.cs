using UnityEngine;
using System.Collections;

public static class ExtensionMethods
{
    /* Timed debug for scriptable objects */
    public static void TimedDebug(this ScriptableObject obj, string msg, float debugInterval = 2)
    {
        var boundary = Time.time - (Time.time % debugInterval);
        if (Time.time < boundary + Time.deltaTime)
            Debug.Log(msg);
    }

    public static void TimedDebugFmt(this ScriptableObject obj, string msg, float debugInterval = 2, params object[] vals)
    {
        var boundary = Time.time - (Time.time % debugInterval);
        if (Time.time < boundary + Time.deltaTime)
            Debug.LogFormat(msg, vals);
    }

    /* Timed debug for monobehaviors */
    public static void TimedDebug(this MonoBehaviour obj, string msg, float debugInterval = 2)
    {
        var boundary = Time.time - (Time.time % debugInterval);
        if (Time.time < boundary + Time.deltaTime)
            Debug.Log(msg);
    }

    public static void TimedDebugFmt(this MonoBehaviour obj, string msg, float debugInterval = 2, params object[] vals)
    {
        var boundary = Time.time - (Time.time % debugInterval);
        if (Time.time < boundary + Time.deltaTime)
            Debug.LogFormat(msg, vals);
    }

    public static Texture2D ToTexture2D(this RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }
}
