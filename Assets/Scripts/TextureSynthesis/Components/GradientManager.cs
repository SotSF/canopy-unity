using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scene singleton holding a named palette of Gradients, authored in the inspector and
/// exposed to the canvas through GradientSelectorNode — the gradient counterpart to
/// VFXRegistry/AudioTrackManager. Nodes serialize only a gradient NAME; the Gradient
/// object itself lives here, so inspector edits propagate live to every consumer
/// (consumers hold the reference and Evaluate it each frame).
/// </summary>
public class GradientManager : Singleton<GradientManager>
{
    [Serializable]
    public class NamedGradient
    {
        public string name;
        public Gradient gradient;
    }

    [Tooltip("Gradients available to GradientSelector nodes, by name.")]
    public List<NamedGradient> gradients = new List<NamedGradient>();

    protected override void OnAwake()
    {
        // Starter palette so the selector works before any authoring. Runtime-only
        // (an auto-created manager isn't saved); add your own in the inspector.
        if (gradients.Count == 0)
        {
            gradients.Add(Make("Heat",
                (Color.black, 0f), (Color.red, 0.4f), (Color.yellow, 0.8f), (Color.white, 1f)));
            gradients.Add(Make("Ocean",
                (new Color(0f, 0.05f, 0.2f), 0f), (Color.cyan, 0.6f), (Color.white, 1f)));
            gradients.Add(Make("Rainbow",
                (Color.red, 0f), (Color.yellow, 0.25f), (Color.green, 0.5f), (Color.cyan, 0.75f), (Color.blue, 1f)));
        }
    }

    static NamedGradient Make(string name, params (Color color, float time)[] keys)
    {
        var g = new Gradient();
        g.SetKeys(
            keys.Select(k => new GradientColorKey(k.color, k.time)).ToArray(),
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return new NamedGradient { name = name, gradient = g };
    }

    public string[] GradientNames =>
        gradients.Where(g => g != null && !string.IsNullOrEmpty(g.name)).Select(g => g.name).ToArray();

    public Gradient Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var entry = gradients.FirstOrDefault(g => g != null && g.name == name);
        return entry?.gradient;
    }

    /// <summary>Bakes a gradient into a horizontal strip texture — shared by the swatch
    /// previews in GradientSelector/GradientMux. Buffer length must match tex width.</summary>
    public static void BakeSwatch(Gradient gradient, Texture2D tex, Color[] buffer)
    {
        if (gradient == null || tex == null || buffer == null || buffer.Length != tex.width) return;
        for (int x = 0; x < buffer.Length; x++)
        {
            buffer[x] = gradient.Evaluate(x / (float)(buffer.Length - 1));
        }
        tex.SetPixels(buffer);
        tex.Apply(false);
    }

    /// <summary>A swatch-sized strip texture with UI-appropriate settings.</summary>
    public static Texture2D CreateSwatchTexture(int width)
    {
        return new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }
}
