using UnityEngine;

public static class ColorUtils
{
    public static Gradient GradientBetween(Color a, Color b)
    {
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(a.a, 0f), new GradientAlphaKey(b.a, 1f) }
        );
        return grad;
    }
}