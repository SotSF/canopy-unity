using UnityEngine;

namespace SecretFire.TextureSynth
{
    // A self-contained single-series sparkline: ring buffer + GPU render via SparklineRenderer.
    // SignalNode has richer per-channel machinery, but it's built around uniform float channels
    // mapped one-per-knob. This helper is for nodes that just want a trace next to an individual
    // output (e.g. GamepadFullController's triggers) without adopting the whole SignalNode base.
    //
    // Usage: Capture(value) + Render(zoom) once per tick (Update phase, NOT during IMGUI), then
    // GUILayout.Box(trace.Texture) during NodeGUI. Call Release() when the owner is destroyed.
    public class SparklineTrace
    {
        static readonly Color BgColor       = new Color(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color ZeroLineColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        readonly int ringSize;
        readonly int displaySamples;
        readonly float captureHz;
        readonly Color lineColor;
        readonly Vector2? fixedRange;

        public int TexHeight { get; }

        readonly float[] ring;
        int head, filled;
        float yMin = -0.5f, yMax = 0.5f;
        float lastCaptureTime = float.NegativeInfinity;
        bool dirty;

        ComputeBuffer buffer;
        RenderTexture texture;
        Material material;

        public RenderTexture Texture => texture;

        public SparklineTrace(Color lineColor, float displaySeconds = 4f, float boundsWindowSeconds = 10f,
            float captureHz = 30f, int texHeight = 18, Vector2? fixedRange = null)
        {
            this.lineColor  = lineColor;
            this.captureHz  = captureHz;
            this.fixedRange = fixedRange;
            TexHeight       = texHeight;
            ringSize        = Mathf.Max(2, Mathf.CeilToInt(boundsWindowSeconds * captureHz));
            displaySamples  = Mathf.Clamp(Mathf.CeilToInt(displaySeconds * captureHz), 2, ringSize);
            ring            = new float[ringSize];
        }

        // Append a sample, throttled to captureHz. Safe to call every frame.
        public void Capture(float v)
        {
            float interval = 1f / Mathf.Max(captureHz, 0.001f);
            if (Time.time - lastCaptureTime < interval) return;
            lastCaptureTime = Time.time;
            if (float.IsNaN(v) || float.IsInfinity(v)) return;

            ring[head] = v;
            head = (head + 1) % ringSize;
            filled = Mathf.Min(filled + 1, ringSize);
            RecomputeRange();
            dirty = true;
        }

        void RecomputeRange()
        {
            if (fixedRange.HasValue) { yMin = fixedRange.Value.x; yMax = fixedRange.Value.y; return; }
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            int oldest = (head - filled + ringSize) % ringSize;
            for (int j = 0; j < filled; j++)
            {
                float s = ring[(oldest + j) % ringSize];
                if (s < lo) lo = s;
                if (s > hi) hi = s;
            }
            float pad = Mathf.Max((hi - lo) * 0.1f, 0.01f);
            yMin = lo - pad;
            yMax = hi + pad;
        }

        void EnsureGpu()
        {
            if (buffer == null)
                buffer = new ComputeBuffer(ringSize, sizeof(float));
            if (texture == null)
            {
                texture = new RenderTexture(displaySamples, TexHeight, 0)
                {
                    hideFlags  = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                };
                texture.Create();
            }
            if (material == null)
                material = SparklineRenderer.CreateMaterial();
        }

        // Push GPU work for the latest samples. Call from the tick (Update phase), not IMGUI.
        public void Render(float zoom = 1f)
        {
            if (!dirty) return;
            EnsureGpu();
            buffer.SetData(ring);
            int displayCount = Mathf.Min(filled, displaySamples);
            SparklineRenderer.Render(
                material, texture, buffer,
                head, displayCount, ringSize,
                new Vector2(yMin, yMax),
                lineColor, BgColor, ZeroLineColor,
                pixelScale: zoom);
            dirty = false;
        }

        public void Release()
        {
            if (buffer != null)   { buffer.Release();  buffer = null; }
            if (texture != null)  { texture.Release(); texture = null; }
            if (material != null) { Object.DestroyImmediate(material); material = null; }
        }
    }
}
