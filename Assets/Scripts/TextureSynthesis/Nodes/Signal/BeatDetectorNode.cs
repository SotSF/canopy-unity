using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

namespace TexSynth.Audio.BeatDetection
{
    /// <summary>
    /// Spectral-flux onset ("beat") detector. Consumes any float[] spectrum stream
    /// (LaspAudioSpectrum, SystemAudioSpectrum, MelFilterbank...); bins just need a
    /// stable per-bin scale frame to frame, which the project's sources provide.
    ///
    /// Each frame: half-wave-rectified spectral flux (per-bin energy increase since the
    /// previous frame, averaged over a selectable bin range) is scored against an
    /// exponentially-tracked mean/deviation of itself. `confidence` continuously outputs
    /// that z-score squashed to 0-1; `beat` fires a one-frame pulse when the z-score
    /// crosses the sensitivity threshold outside a refractory interval. Restricting the
    /// bin range to the low end makes it a kick detector. Follows KeySignal/MIDINote
    /// pulse semantics, so `beat` can drive EnvelopeGenerator directly.
    /// </summary>
    [Node(false, "Signal/BeatDetector")]
    public class BeatDetectorNode : SignalNode
    {
        public override string GetID => "BeatDetectorNode";
        public override string Title { get { return "BeatDetector"; } }

        private Vector2 _DefaultSize = new Vector2(200, 160);
        protected override Vector2 BaseDefaultSize => _DefaultSize;

        [ValueConnectionKnob("spectrumData", Direction.In, typeof(float[]), NodeSide.Left)]
        public ValueConnectionKnob spectrumDataKnob;

        [ValueConnectionKnob("beat", Direction.Out, typeof(bool), NodeSide.Right)]
        public ValueConnectionKnob beatKnob;

        [ValueConnectionKnob("confidence", Direction.Out, typeof(float), NodeSide.Right)]
        public ValueConnectionKnob confidenceKnob;

        protected override IEnumerable<SignalChannel> GetSignalChannels()
        {
            yield return new SignalChannel
            {
                outputKnob = confidenceKnob,
                getValue   = () => confidence,
                label      = "confidence",
            };
        }

        // Beat threshold in standard deviations of recent flux
        public float sensitivity = 2.0f;
        // Refractory period: no two beats closer than this (seconds). 0.15s caps at 400 BPM.
        public float minBeatInterval = 0.15f;
        // Bin range to watch, BandAvg-style. highEnd <= 0 means "full range" until a
        // spectrum arrives (also what legacy saves deserialize to).
        public int filterLowEnd = 0;
        public int filterHighEnd = 0;

        private int spectrumSize;

        // Detection state. Flux statistics are tracked as exponential moving mean/variance
        // so the threshold adapts to material loudness and spectrum-source scale.
        [System.NonSerialized] private float[] prevSpectrum;
        [System.NonSerialized] private float fluxMean;
        [System.NonSerialized] private float fluxVar;
        [System.NonSerialized] private bool statsInitialized;
        [System.NonSerialized] private float confidence;
        [System.NonSerialized] private float zScore;
        [System.NonSerialized] private double lastBeatTime = double.NegativeInfinity;
        [System.NonSerialized] private float bpmEstimate;
        // One-tick pulse stays true for every Calculate within its frame: OnNodeChange can
        // re-run Calculate in-frame, and consuming on the first call would hide the pulse
        // from the re-runs downstream nodes actually read.
        [System.NonSerialized] private int beatFrame = -1;
        // Detection state must advance once per rendered frame even though Calculate can
        // re-run within a frame (re-running would compare a spectrum against itself and
        // double-advance the statistics).
        [System.NonSerialized] private int lastProcessedFrame = -1;

        // Statistics adaptation horizon (seconds). Long enough that a whole beat cycle
        // fits without the mean chasing individual onsets.
        const float StatsTau = 2.0f;
        // Deviation floor in flux units so silence (variance ~ 0) can't turn noise into
        // huge z-scores. Spectrum sources here are normalized roughly 0-1 per bin.
        const float DeviationFloor = 1e-3f;
        // z-score that maps to confidence 1.0
        const float ConfidenceFullScaleZ = 4f;

        void ProcessFrame(float[] spectrum)
        {
            spectrumSize = spectrum.Length;
            if (filterHighEnd <= 0) filterHighEnd = spectrum.Length;
            int lo = Mathf.Clamp(filterLowEnd, 0, spectrum.Length);
            int hi = Mathf.Clamp(filterHighEnd, lo, spectrum.Length);

            if (prevSpectrum == null || prevSpectrum.Length != spectrum.Length)
            {
                // First frame or source resolution change: no valid delta, restart stats
                prevSpectrum = (float[])spectrum.Clone();
                statsInitialized = false;
                return;
            }

            // Half-wave rectified spectral flux: energy increases only, so decays don't
            // cancel attacks. Mean over the bin range keeps the scale independent of
            // range width and source resolution.
            float flux = 0;
            for (int i = lo; i < hi; i++)
            {
                float d = spectrum[i] - prevSpectrum[i];
                if (d > 0) flux += d;
            }
            flux = hi > lo ? flux / (hi - lo) : 0f;

            System.Array.Copy(spectrum, prevSpectrum, spectrum.Length);

            if (!statsInitialized)
            {
                fluxMean = flux;
                fluxVar = 0;
                statsInitialized = true;
                return;
            }

            // Score against stats from *before* this frame, then fold the frame in
            float deviation = Mathf.Sqrt(fluxVar) + DeviationFloor;
            zScore = (flux - fluxMean) / deviation;
            confidence = Mathf.Clamp01(zScore / ConfidenceFullScaleZ);

            float alpha = 1f - Mathf.Exp(-Time.deltaTime / StatsTau);
            float delta = flux - fluxMean;
            fluxMean += alpha * delta;
            fluxVar = (1f - alpha) * (fluxVar + alpha * delta * delta);

            double now = Time.realtimeSinceStartupAsDouble;
            if (zScore >= sensitivity && now - lastBeatTime >= minBeatInterval)
            {
                beatFrame = Time.frameCount;
                double interval = now - lastBeatTime;
                // Inter-beat intervals in musical range feed a smoothed BPM readout
                if (interval > 0.2 && interval < 2.0)
                {
                    float instBpm = (float)(60.0 / interval);
                    bpmEstimate = bpmEstimate <= 0 ? instBpm : Mathf.Lerp(bpmEstimate, instBpm, 0.2f);
                }
                lastBeatTime = now;
            }
        }

        public override void NodeGUI()
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            spectrumDataKnob.DisplayLayout();
            GUILayout.FlexibleSpace();
            // Beat lamp + rough BPM readout (from raw inter-beat intervals, so it
            // wanders more than a MIDI clock; informational only)
            bool beatRecent = Time.realtimeSinceStartupAsDouble - lastBeatTime < 0.1;
            GUILayout.Label(beatRecent ? "●" : "○", GUILayout.Width(18));
            GUILayout.Label(bpmEstimate > 0 ? $"~{bpmEstimate:0} BPM" : "--- BPM", GUILayout.Width(70));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Sensitivity", GUILayout.Width(70));
            sensitivity = RTEditorGUI.Slider(sensitivity, 0.5f, 5f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Min gap (s)", GUILayout.Width(70));
            minBeatInterval = RTEditorGUI.Slider(minBeatInterval, 0.05f, 1f);
            GUILayout.EndHorizontal();

            // Watched bin range, BandAvg-style (narrow to the low bins for kick detection)
            filterLowEnd = RTEditorGUI.IntSlider(filterLowEnd, 0, Mathf.Max(filterHighEnd, 1));
            filterHighEnd = RTEditorGUI.IntSlider(filterHighEnd, filterLowEnd, Mathf.Max(spectrumSize, 1));

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            beatKnob.DisplayLayout();
            GUILayout.EndHorizontal();

            DrawSparkline();
            GUILayout.EndVertical();

            if (GUI.changed)
                NodeEditor.curNodeCanvas.OnNodeChange(this);
        }

        public override bool DoCalc()
        {
            var spectrum = spectrumDataKnob.connected() ? spectrumDataKnob.GetValue<float[]>() : null;
            if (spectrum != null && spectrum.Length > 0 && Time.frameCount != lastProcessedFrame)
            {
                ProcessFrame(spectrum);
                lastProcessedFrame = Time.frameCount;
            }

            beatKnob.SetValue(Time.frameCount == beatFrame);
            confidenceKnob.SetValue(confidence);
            return true;
        }
    }
}
