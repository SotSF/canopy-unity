using System;
using System.Collections.Generic;
using NodeEditorFramework;
using UnityEngine;

namespace SecretFire.TextureSynth
{
    public abstract class SignalNode : TickingNode
    {
        // Subclasses describe each (port, sparkline-trace) pair via GetSignalChannels().
        // outputKnob may be null (sparkline-only); getValue may be null (port-only row).
        protected struct SignalChannel
        {
            public ValueConnectionKnob outputKnob;
            public Func<float> getValue;
            public string label;
            public Color color;
        }

        protected abstract IEnumerable<SignalChannel> GetSignalChannels();

        static readonly Color DefaultBgColor       = new Color(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color DefaultZeroLineColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        static readonly Color[] DefaultPalette = {
            new Color(0.40f, 0.85f, 1.00f, 1f),
            new Color(1.00f, 0.55f, 0.30f, 1f),
            new Color(0.55f, 1.00f, 0.45f, 1f),
            new Color(1.00f, 0.45f, 0.85f, 1f),
            new Color(1.00f, 0.95f, 0.40f, 1f),
        };

        // Bounds-tracking ring covers a longer window than the displayed slice so
        // y-axis scale stays stable for periodic signals whose period fits within it.
        protected virtual float SparklineDisplaySeconds      => 4f;
        protected virtual float SparklineBoundsWindowSeconds => 10f;
        protected virtual float SparklineCaptureHz           => 30f;
        protected virtual int   SparklineTexHeight           => 24;
        protected virtual bool  ShowSparkline                => true;

        public bool sparklineCollapsed = false;

        [NonSerialized] ChannelState[] channelStates;
        [NonSerialized] bool channelsInitialized;
        [NonSerialized] float lastCaptureTime = float.NegativeInfinity;

        class ChannelState
        {
            public int ringSize;
            public int displaySamples;
            public float[] ring;
            public int head;
            public int filled;
            public float yMin = -0.5f;
            public float yMax =  0.5f;
            public bool dirty;
            public ComputeBuffer buffer;
            public RenderTexture texture;
            public Material material;
        }

        const float SparklineToggleRowHeight    = 22f;
        const float SparklineCollapsedRowHeight = 20f;
        const float SparklineLabelColumnWidth   = 60f;
        const float SparklineValueColumnWidth   = 50f;
        const float SparklineKnobColumnWidth    = 60f;
        const float SparklineKnobColumnWidthMax = 130f;
        const float SparklineRowPadding         = 8f;
        const float SparklineMinTexWidth        = 40f;
        protected virtual float SparklineExpandedRowHeight => SparklineTexHeight + 6f;

        // Right-aligned, non-wrapping style for the per-row knob label. Reusing the framework's
        // nodeLabelRight but with wordWrap off so long output names (e.g. "Tap/Bounce") occupy a
        // single line in a fixed-width column instead of wrapping and inflating row height.
        static GUIStyle _knobLabelStyle;
        static GUIStyle KnobLabelStyle
        {
            get
            {
                if (_knobLabelStyle == null)
                {
                    var basis = NodeEditorGUI.nodeLabelRight ?? GUI.skin.label;
                    _knobLabelStyle = new GUIStyle(basis) { wordWrap = false, clipping = TextClipping.Clip };
                }
                return _knobLabelStyle;
            }
        }

        protected int   DisplaySampleCount => Mathf.CeilToInt(SparklineDisplaySeconds * SparklineCaptureHz);
        // Just enough room for a meaningfully-sized sparkline plus the port column.
        // The texture itself flexes to fill whatever space is available beyond this.
        protected float SparklineRowMinWidth =>
            SparklineKnobColumnWidth + SparklineMinTexWidth + SparklineValueColumnWidth + SparklineRowPadding;

        protected virtual Vector2 BaseDefaultSize => new Vector2(200, 100);
        protected virtual Vector2 BaseMinSize     => new Vector2(100, 50);

        public override Vector2 DefaultSize
        {
            get
            {
                var sz = BaseDefaultSize;
                if (ShowSparkline)
                {
                    sz.y += SparklineToggleRowHeight;
                    foreach (var desc in BuildChannelDescs())
                        sz.y += RowHeightFor(desc);
                    if (!sparklineCollapsed) sz.x = Mathf.Max(sz.x, SparklineRowMinWidth);
                }
                return sz;
            }
        }

        // Sparkline rows are tall (texture); port-only rows stay compact.
        float RowHeightFor(SignalChannel desc)
        {
            bool drawingTexture = !sparklineCollapsed && desc.getValue != null;
            return drawingTexture ? SparklineExpandedRowHeight : SparklineCollapsedRowHeight;
        }

        public override Vector2 MinSize
        {
            get
            {
                var min = BaseMinSize;
                if (ShowSparkline && !sparklineCollapsed)
                    min.x = Mathf.Max(min.x, SparklineRowMinWidth);
                return min;
            }
        }

        public override bool Calculate()
        {
            bool wasInitialized = initialized;
            bool result = base.Calculate();
            if (wasInitialized && result && ShowSparkline) CaptureSamples();
            return result;
        }

        // Rebuilt on every call so that outputKnob refs picked up via reflection
        // after Node.Create() finishes (it sets autoSize from DefaultSize before
        // calling UpdateConnectionPorts) are seen as soon as they're populated.
        SignalChannel[] BuildChannelDescs()
        {
            var list = new List<SignalChannel>();
            int idx = 0;
            foreach (var ch in GetSignalChannels())
            {
                var copy = ch;
                if (copy.color == default(Color)) copy.color = DefaultPalette[idx % DefaultPalette.Length];
                if (string.IsNullOrEmpty(copy.label)) copy.label = $"ch{idx}";
                list.Add(copy);
                idx++;
            }
            return list.ToArray();
        }

        void EnsureChannels()
        {
            if (channelsInitialized) return;
            var descs = BuildChannelDescs();
            int ringSize = Mathf.Max(2, Mathf.CeilToInt(SparklineBoundsWindowSeconds * SparklineCaptureHz));
            int displaySamples = Mathf.Clamp(
                Mathf.CeilToInt(SparklineDisplaySeconds * SparklineCaptureHz),
                2, ringSize);
            channelStates = new ChannelState[descs.Length];
            for (int i = 0; i < descs.Length; i++)
            {
                channelStates[i] = new ChannelState
                {
                    ringSize       = ringSize,
                    displaySamples = displaySamples,
                    ring           = new float[ringSize],
                };
            }
            channelsInitialized = true;
        }

        void EnsureGpuResources(ChannelState ch)
        {
            if (ch.buffer == null)
                ch.buffer = new ComputeBuffer(ch.ringSize, sizeof(float));
            if (ch.texture == null)
            {
                ch.texture = new RenderTexture(ch.displaySamples, SparklineTexHeight, 0)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                };
                ch.texture.Create();
            }
            if (ch.material == null)
                ch.material = SparklineRenderer.CreateMaterial();
        }

        protected virtual void CaptureSamples()
        {
            float interval = 1f / Mathf.Max(SparklineCaptureHz, 0.001f);
            if (Time.time - lastCaptureTime < interval) return;
            lastCaptureTime = Time.time;

            EnsureChannels();
            var descs = BuildChannelDescs();
            for (int c = 0; c < channelStates.Length && c < descs.Length; c++)
            {
                var desc = descs[c];
                if (desc.getValue == null) continue;
                var ch = channelStates[c];
                float v;
                try { v = desc.getValue(); }
                catch { continue; }
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;

                ch.ring[ch.head] = v;
                ch.head = (ch.head + 1) % ch.ringSize;
                ch.filled = Mathf.Min(ch.filled + 1, ch.ringSize);

                RecomputeRange(ch);
                ch.dirty = true;
            }

            if (!sparklineCollapsed) RenderSparklines();
        }

        // Renders dirty channels into their target textures. Called from
        // CaptureSamples (in Update phase) so GPU work never happens during IMGUI.
        void RenderSparklines()
        {
            float zoom = (NodeEditor.curEditorState != null)
                ? NodeEditor.curEditorState.zoom
                : 1f;
            var descs = BuildChannelDescs();
            for (int i = 0; i < channelStates.Length && i < descs.Length; i++)
            {
                var ch = channelStates[i];
                if (!ch.dirty) continue;
                if (descs[i].getValue == null) continue;
                EnsureGpuResources(ch);
                ch.buffer.SetData(ch.ring);
                int displayCount = Mathf.Min(ch.filled, ch.displaySamples);
                SparklineRenderer.Render(
                    ch.material, ch.texture, ch.buffer,
                    ch.head, displayCount, ch.ringSize,
                    new Vector2(ch.yMin, ch.yMax),
                    descs[i].color, DefaultBgColor, DefaultZeroLineColor,
                    pixelScale: zoom);
                ch.dirty = false;
            }
        }

        static void RecomputeRange(ChannelState ch)
        {
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            int oldest = (ch.head - ch.filled + ch.ringSize) % ch.ringSize;
            for (int j = 0; j < ch.filled; j++)
            {
                float s = ch.ring[(oldest + j) % ch.ringSize];
                if (s < lo) lo = s;
                if (s > hi) hi = s;
            }
            float pad = Mathf.Max((hi - lo) * 0.1f, 0.01f);
            ch.yMin = lo - pad;
            ch.yMax = hi + pad;
        }

        protected void DrawSparkline()
        {
            if (!ShowSparkline) return;
            EnsureChannels();
            var descs = BuildChannelDescs();

            GUILayout.BeginHorizontal();
            string toggleLabel = sparklineCollapsed ? "▶" : "▼";
            if (GUILayout.Button(toggleLabel, GUILayout.Width(22))) sparklineCollapsed = !sparklineCollapsed;
            GUILayout.Label("Sparkline");
            GUILayout.EndHorizontal();

            // A uniform knob-label column keeps every sparkline texture the same width and stops
            // longer output names from wrapping. Sized to the widest name (clamped), measured here
            // because CalcSize needs the active GUI skin.
            float knobColumnWidth = SparklineKnobColumnWidth;
            foreach (var desc in descs)
            {
                if (desc.outputKnob == null) continue;
                knobColumnWidth = Mathf.Max(knobColumnWidth,
                    KnobLabelStyle.CalcSize(new GUIContent(desc.outputKnob.name)).x);
            }
            knobColumnWidth = Mathf.Min(knobColumnWidth, SparklineKnobColumnWidthMax);

            for (int i = 0; i < channelStates.Length && i < descs.Length; i++)
            {
                var ch = channelStates[i];
                var desc = descs[i];
                EnsureGpuResources(ch);
                GUILayout.BeginHorizontal(GUILayout.MinHeight(RowHeightFor(desc)));
                // Leading label is only useful for sparkline-only channels (no knob to
                // identify the trace via its own label).
                if (desc.outputKnob == null && !string.IsNullOrEmpty(desc.label))
                    GUILayout.Label(desc.label, GUILayout.Width(SparklineLabelColumnWidth));
                if (!sparklineCollapsed && desc.getValue != null)
                    GUILayout.Box(ch.texture,
                        GUILayout.ExpandWidth(true),
                        GUILayout.MinWidth(SparklineMinTexWidth),
                        GUILayout.Height(ch.texture.height));
                else
                    GUILayout.FlexibleSpace();
                if (desc.getValue != null)
                {
                    float v;
                    try { v = desc.getValue(); }
                    catch { v = 0f; }
                    GUILayout.Label(v.ToString("0.000"), GUILayout.Width(SparklineValueColumnWidth));
                }
                if (desc.outputKnob != null)
                {
                    // Render the label in a fixed-width column, then let the knob place itself
                    // against it (knob y-position comes from the label rect; width is uniform).
                    GUILayout.Label(desc.outputKnob.name, KnobLabelStyle, GUILayout.Width(knobColumnWidth));
                    desc.outputKnob.SetPosition();
                }
                GUILayout.EndHorizontal();
            }
        }

        public virtual void OnDestroy()
        {
            if (channelStates == null) return;
            foreach (var ch in channelStates)
            {
                if (ch == null) continue;
                if (ch.buffer  != null) ch.buffer.Release();
                if (ch.texture != null) ch.texture.Release();
                if (ch.material != null) UnityEngine.Object.DestroyImmediate(ch.material);
            }
            channelStates = null;
            channelsInitialized = false;
        }
    }
}
