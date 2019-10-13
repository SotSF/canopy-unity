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
}
