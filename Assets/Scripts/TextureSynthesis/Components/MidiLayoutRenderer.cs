using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Renders a stylized picture of a MIDI controller layout into a RenderTexture with the
/// bound control highlighted, so nodes can show WHERE on the physical device a numeric
/// CC id lives. GPU work happens only when called (bind changes), never per frame.
/// </summary>
public static class MidiLayoutRenderer
{
    public const int TexWidth = 200;
    public const int TexHeight = 150;

    [StructLayout(LayoutKind.Sequential)]
    struct ControlGpu
    {
        public Vector2 pos;
        public Vector2 size;
        public int shape;    // 0 knob, 1 skinny knob, 2 fader, 3 button
        public int colorIdx;
        public int state;    // 0 normal, 1 highlighted, 2 ghost
        public int pad;
    }

    static ComputeShader shader;
    static int kernelId;
    static readonly List<ControlGpu> controlScratch = new List<ControlGpu>();

    // Indexed by AkaiMidiMixLayout.CcColor
    static readonly Vector4[] Palette =
    {
        new Vector4(0.83f, 0.66f, 0.21f, 1f), // gold
        new Vector4(0.72f, 0.72f, 0.76f, 1f), // silver
        new Vector4(0.16f, 0.16f, 0.16f, 1f), // black
        new Vector4(0.30f, 0.12f, 0.12f, 1f), // blackred
        new Vector4(0.85f, 0.25f, 0.22f, 1f), // red
        new Vector4(0.35f, 0.80f, 0.40f, 1f), // green
        new Vector4(0.30f, 0.50f, 0.95f, 1f), // blue
        new Vector4(0.92f, 0.92f, 0.92f, 1f), // white
        new Vector4(0.65f, 0.40f, 0.90f, 1f), // purple
        new Vector4(0.95f, 0.60f, 0.25f, 1f), // orange
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        shader = null;
    }

    public static RenderTexture CreateTexture()
    {
        var tex = new RenderTexture(TexWidth, TexHeight, 0)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.Create();
        return tex;
    }

    /// <summary>
    /// Renders the MIDIMix picture into target, highlighting highlightCcId (-1 = none).
    /// timeSeconds drives the contracting bulls-eye pulse — pass Time.time and re-render
    /// per frame while visible for animation.
    /// </summary>
    public static void Render(AkaiMidiMixLayout layout, int highlightCcId, RenderTexture target, float timeSeconds = 0f)
    {
        if (layout == null || target == null) return;
        if (shader == null)
        {
            shader = Resources.Load<ComputeShader>("NodeShaders/MidiLayoutView");
            if (shader == null)
            {
                Debug.LogError("MidiLayoutRenderer: NodeShaders/MidiLayoutView.compute not found");
                return;
            }
            kernelId = shader.FindKernel("DrawLayout");
        }

        controlScratch.Clear();
        BuildMidiMixControls(layout, highlightCcId, controlScratch);

        var buffer = new ComputeBuffer(controlScratch.Count, Marshal.SizeOf<ControlGpu>());
        buffer.SetData(controlScratch);
        shader.SetBuffer(kernelId, "Controls", buffer);
        shader.SetInt("controlCount", controlScratch.Count);
        shader.SetInt("oWidth", target.width);
        shader.SetInt("oHeight", target.height);
        shader.SetFloat("timeSeconds", timeSeconds);
        shader.SetVectorArray("Palette", Palette);
        shader.SetTexture(kernelId, "OutputTex", target);
        shader.Dispatch(kernelId, Mathf.CeilToInt(target.width / 16f), Mathf.CeilToInt(target.height / 16f), 1);
        buffer.Release();
    }

    // Pixel geometry for the 200x150 MIDIMix picture. Texture y=0 is the BOTTOM row as
    // displayed by GUI, so knobs (physically on top) get large y values.
    static void BuildMidiMixControls(AkaiMidiMixLayout layout, int highlightCcId, List<ControlGpu> outControls)
    {
        float ColX(int col) => 14f + col * 20f;

        // ghost buttons for spatial context: 8x2 grid below the knobs + right-side column
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                outControls.Add(new ControlGpu
                {
                    pos = new Vector2(ColX(col), 72f - row * 14f),
                    size = new Vector2(6f, 4f),
                    shape = 3,
                    colorIdx = (int)AkaiMidiMixLayout.CcColor.silver,
                    state = 2,
                });
            }
        }
        for (int i = 0; i < 3; i++)
        {
            outControls.Add(new ControlGpu
            {
                pos = new Vector2(186f, 132f - i * 20f),
                size = new Vector2(6f, 4f),
                shape = 3,
                colorIdx = (int)AkaiMidiMixLayout.CcColor.silver,
                state = 2,
            });
        }

        foreach (var desc in layout.AllControls)
        {
            var ctrl = new ControlGpu
            {
                colorIdx = (int)desc.color,
                state = desc.id == highlightCcId ? 1 : 0,
            };
            if (desc.ccType == AkaiMidiMixLayout.CcType.fader)
            {
                // faders sit in the dark bottom strip; caps at mid-travel
                ctrl.pos = new Vector2(ColX(desc.position.x), 27f);
                ctrl.size = new Vector2(7f, 3f);
                ctrl.shape = 2;
            }
            else
            {
                bool skinny = desc.ccType == AkaiMidiMixLayout.CcType.skinnyknob;
                // knob grid rows 0..2 top to bottom
                ctrl.pos = new Vector2(ColX(desc.position.x), 134f - desc.position.y * 22f);
                ctrl.size = new Vector2(skinny ? 5f : 7.5f, 0f);
                ctrl.shape = skinny ? 1 : 0;
            }
            outControls.Add(ctrl);
        }
    }
}
