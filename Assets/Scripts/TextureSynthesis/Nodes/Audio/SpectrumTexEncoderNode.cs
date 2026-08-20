using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Encodes a spectrum (any float[] — raw FFT bins, mel bands, log spectrum...) into an
/// Nx1 single-channel float texture, one texel per bin, so downstream effects and
/// shaders can consume audio data on the GPU. RFloat format: values pass through
/// unclamped and unquantized; sample the R channel. Texel-center convention applies as
/// everywhere in this project: bin i lives at u = (i + 0.5) / N. Bilinear filtering
/// (default) gives shaders a smooth curve across bins; switch to point sampling for
/// exact per-bin reads (e.g. discrete bar visualizations).
///
/// A second output encodes the per-bin frame-to-frame derivative (signed: attacks
/// positive, decays negative — RFloat carries negatives intact). "Per second" divides
/// by deltaTime for framerate-independent units; off gives the raw per-frame delta.
/// </summary>
[Node(false, "Audio/SpectrumTexEncoder")]
public class SpectrumTexEncoderNode : TickingNode
{
    public override string GetID => "SpectrumTexEncoderNode";
    public override string Title { get { return "SpectrumTexEncoder"; } }

    private Vector2 _DefaultSize = new Vector2(180, 150);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.In, typeof(float[]), NodeSide.Left)]
    public ValueConnectionKnob spectrumDataKnob;

    [ValueConnectionKnob("out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("derivative", Direction.Out, typeof(Texture), NodeSide.Bottom, 110)]
    public ValueConnectionKnob derivativeOutputKnob;

    public bool bilinear = true;
    public bool perSecond = false;

    [System.NonSerialized] private Texture2D tex;
    [System.NonSerialized] private Texture2D derivTex;
    [System.NonSerialized] private float[] prevSpectrum;
    [System.NonSerialized] private float[] derivBuffer;
    // Texture uploads happen once per frame on the Update-phase tick (OnNodeChange can
    // re-run Calculate mid-OnGUI: GPU uploads while the GUI is drawing the preview make
    // it flicker, and a re-run must not difference the spectrum against itself)
    [System.NonSerialized] private int lastUploadFrame = -1;

    private void OnDestroy()
    {
        if (tex != null)
        {
            UnityEngine.Object.DestroyImmediate(tex);
            tex = null;
        }
        if (derivTex != null)
        {
            UnityEngine.Object.DestroyImmediate(derivTex);
            derivTex = null;
        }
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    static void EnsureTexture(ref Texture2D t, int bins)
    {
        if (t != null && t.width == bins) return;
        if (t != null) UnityEngine.Object.DestroyImmediate(t);
        t = new Texture2D(bins, 1, TextureFormat.RFloat, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        spectrumDataKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.Label(tex != null ? $"{tex.width}x1 RFloat" : "no input");
        GUILayout.EndHorizontal();

        bilinear = RTEditorGUI.Toggle(bilinear,
            new GUIContent("Smooth", "Bilinear filtering across bins; off = exact per-texel (point) sampling"));
        perSecond = RTEditorGUI.Toggle(perSecond,
            new GUIContent("Per second", "Divide the derivative by deltaTime (framerate-independent); off = raw per-frame delta"));

        // Preview strips (render in the red channel — RFloat has no G/B; the derivative
        // preview only shows its positive half, negatives clamp to black)
        if (tex != null)
        {
            GUILayout.Box(tex, GUILayout.ExpandWidth(true), GUILayout.Height(14));
        }
        if (derivTex != null)
        {
            GUILayout.Box(derivTex, GUILayout.ExpandWidth(true), GUILayout.Height(14));
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        var spectrum = spectrumDataKnob.connected() ? spectrumDataKnob.GetValue<float[]>() : null;
        if (spectrum != null && spectrum.Length > 0)
        {
            EnsureTexture(ref tex, spectrum.Length);
            EnsureTexture(ref derivTex, spectrum.Length);
            tex.filterMode = derivTex.filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point;
            if (Time.frameCount != lastUploadFrame)
            {
                if (prevSpectrum == null || prevSpectrum.Length != spectrum.Length)
                {
                    // First frame or bin-count change: no valid delta, emit zeros
                    prevSpectrum = new float[spectrum.Length];
                    derivBuffer = new float[spectrum.Length];
                    System.Array.Copy(spectrum, prevSpectrum, spectrum.Length);
                }
                else
                {
                    float scale = perSecond ? 1f / Mathf.Max(Time.deltaTime, 1e-4f) : 1f;
                    for (int i = 0; i < spectrum.Length; i++)
                    {
                        derivBuffer[i] = (spectrum[i] - prevSpectrum[i]) * scale;
                    }
                    System.Array.Copy(spectrum, prevSpectrum, spectrum.Length);
                }
                tex.SetPixelData(spectrum, 0);
                tex.Apply(false);
                derivTex.SetPixelData(derivBuffer, 0);
                derivTex.Apply(false);
                lastUploadFrame = Time.frameCount;
            }
        }
        textureOutputKnob.SetValue<Texture>(tex);
        derivativeOutputKnob.SetValue<Texture>(derivTex);
        return true;
    }
}
