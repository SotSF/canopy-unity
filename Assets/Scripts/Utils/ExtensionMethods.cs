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

    public static Texture2D ToTexture2D(this RenderTexture rTex, TextureFormat format = TextureFormat.RGB24)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, format, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

    /* Per-renderer shader property overrides. Assigning through renderer.material instances
       a new Material that is NOT destroyed with the GameObject and leaks native memory;
       property blocks override without instancing. The block applies to every material on
       the renderer (e.g. a ParticleSystemRenderer's particle and trail materials at once). */
    private static MaterialPropertyBlock propertyBlock;

    public static void SetColor(this Renderer renderer, string property, Color color)
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(property, color);
        renderer.SetPropertyBlock(propertyBlock);
    }

    public static void SetFloat(this Renderer renderer, string property, float value)
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(property, value);
        renderer.SetPropertyBlock(propertyBlock);
    }
}
