using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Collections.Generic;

using UnityEngine;

// One-shot ADSR + hold envelope driven by rising-edge triggers. Designed to convert a one-tick
// pulse (e.g. BeaconGame's levelUp / collect) into a shaped float curve that can drive continuous
// parameters (FluidSim's dyeLevel, HSV brightness, Pan speed, ...).
//
// Three outputs, three semantics:
//   value: 0..1 shaped curve — for continuous float consumers (dyeLevel, brightness, ...)
//   gate : bool level, true while value > threshold — for level-semantics bool consumers (Run, ...)
//   onset: bool, high for exactly one tick when a trigger fires — for event-semantics bool
//          consumers (ApplyDye, ApplyForce, Reset, ...). Wiring `gate` to these would fire
//          them every tick and quickly saturate whatever they feed.
//
// Trigger semantics: input is a float, sampled tick-to-tick; a low→high transition across
// TriggerHighThreshold restarts the envelope from its current value (smooth retrigger, no click).
// Sustain is a *level*, not a duration; the "hold" parameter is how long we sit at that level
// before releasing. This lets a one-shot trigger drive a full ADSR-shaped curve — classic ADSR
// would need a separate gate-off signal for that, which our pulse triggers don't provide.
[Node(false, "Signal/EnvelopeGenerator")]
public class EnvelopeGeneratorNode : SignalNode
{
    public override string GetID => "EnvelopeGeneratorNode";
    public override string Title { get { return "EnvelopeGenerator"; } }

    private Vector2 _DefaultSize = new Vector2(260, 280);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("trigger", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob triggerKnob;

    [ValueConnectionKnob("attack", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob attackKnob;

    [ValueConnectionKnob("decay", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob decayKnob;

    [ValueConnectionKnob("sustain", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob sustainKnob;

    [ValueConnectionKnob("hold", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob holdKnob;

    [ValueConnectionKnob("release", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob releaseKnob;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    [ValueConnectionKnob("gate", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob gateKnob;

    [ValueConnectionKnob("onset", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob onsetKnob;

    // Time parameters in milliseconds so short attacks (5..500ms) type as friendly integers
    // rather than 0.005..0.5. Converted to seconds via *Sec helpers at the point of use.
    // Inspector fallbacks used when the matching input knob is unconnected.
    public float attackMs = 100f;
    public float decayMs = 200f;
    public float sustain = 0.6f;
    public float holdMs = 500f;
    public float releaseMs = 800f;
    public float gateThreshold = 0.05f;

    private float AttackSec  => attackMs  * 0.001f;
    private float DecaySec   => decayMs   * 0.001f;
    private float HoldSec    => holdMs    * 0.001f;
    private float ReleaseSec => releaseMs * 0.001f;

    // Any incoming trigger value at or above this counts as "high"; a low→high crossing fires.
    // 0.5 rather than 0.0 so noisy near-zero float traffic can't spuriously retrigger.
    private const float TriggerHighThreshold = 0.5f;

    private enum Phase { Idle, Attack, Decay, Hold, Release }
    private Phase phase = Phase.Idle;
    private float phaseTime;         // seconds elapsed in the current phase
    private float phaseStartValue;   // envelope value when this phase began (so retriggers don't click)
    private float outValue;
    private float prevTriggerValue;
    private float lastTickTime;
    private bool haveLastTick;

    // Set by the "Trigger" button in NodeGUI; consumed once by DoCalc and cleared, so a single
    // click fires exactly one envelope regardless of how many repaints happen in between.
    [System.NonSerialized] private bool manualTriggerRequested;

    // Static preview graph: rebuilt only when parameters actually change so we're not thrashing
    // GPU uploads every repaint. Cache prev values with sentinels so first call always redraws.
    [System.NonSerialized] private Texture2D previewTex;
    [System.NonSerialized] private float prevPreviewAttackMs = float.NaN;
    [System.NonSerialized] private float prevPreviewDecayMs;
    [System.NonSerialized] private float prevPreviewSustain;
    [System.NonSerialized] private float prevPreviewHoldMs;
    [System.NonSerialized] private float prevPreviewReleaseMs;

    private const int PreviewWidth = 200;
    private const int PreviewHeight = 56;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = valueKnob,
            getValue = () => outValue,
            label = "value",
        };
        yield return new SignalChannel
        {
            outputKnob = gateKnob,
            getValue = () => outValue > gateThreshold ? 1f : 0f,
            label = "gate",
        };
        // Onset shows as a sparkline too so you can see the one-tick pulses land visually.
        // getValue polls the *last-computed* onset flag rather than a live consumable event, so
        // capturing on the sparkline schedule doesn't interfere with the actual pulse output.
        yield return new SignalChannel
        {
            outputKnob = onsetKnob,
            getValue = () => lastOnsetForSparkline ? 1f : 0f,
            label = "onset",
        };
    }

    // Mirror of the onset pulse from the most recent DoCalc, so the sparkline getter (which can
    // fire at any time) reflects "did onset just fire" without racing DoCalc's per-tick reset.
    [System.NonSerialized] private bool lastOnsetForSparkline;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        // Trigger row: port + manual-fire button + current-phase readout. The button feels like
        // a keyboard-style momentary trigger, useful for testing envelope shape without wiring
        // up a source.
        GUILayout.BeginHorizontal();
        triggerKnob.DisplayLayout();
        if (GUILayout.Button("Trigger", GUILayout.Width(70)))
            manualTriggerRequested = true;
        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("{0}", phase), GUILayout.Width(80));
        GUILayout.EndHorizontal();

        FloatKnobOrField("Attack (ms)",  ref attackMs,  attackKnob);
        FloatKnobOrField("Decay (ms)",   ref decayMs,   decayKnob);
        FloatKnobOrField("Sustain",      ref sustain,   sustainKnob);
        FloatKnobOrField("Hold (ms)",    ref holdMs,    holdKnob);
        FloatKnobOrField("Release (ms)", ref releaseMs, releaseKnob);

        DrawEnvelopePreview();

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        // Connected knobs override their inspector fallbacks each tick, so upstream envelopes
        // / MIDI can modulate the shape live. Time-knob values are interpreted as ms to match
        // the field's unit — a chained envelope feeding attackKnob should send ms.
        if (attackKnob.connected())  attackMs  = attackKnob.GetValue<float>();
        if (decayKnob.connected())   decayMs   = decayKnob.GetValue<float>();
        if (sustainKnob.connected()) sustain   = sustainKnob.GetValue<float>();
        if (holdKnob.connected())    holdMs    = holdKnob.GetValue<float>();
        if (releaseKnob.connected()) releaseMs = releaseKnob.GetValue<float>();

        // Guard against pathological inputs that would divide-by-zero or reverse-time the phase.
        sustain   = Mathf.Clamp01(sustain);
        attackMs  = Mathf.Max(0f, attackMs);
        decayMs   = Mathf.Max(0f, decayMs);
        holdMs    = Mathf.Max(0f, holdMs);
        releaseMs = Mathf.Max(0f, releaseMs);

        // Rising-edge detection on the wired trigger: previous tick sub-threshold, this tick
        // supra-threshold.
        float trigVal = triggerKnob.connected() ? triggerKnob.GetValue<float>() : 0f;
        bool wasHigh = prevTriggerValue >= TriggerHighThreshold;
        bool nowHigh = trigVal >= TriggerHighThreshold;
        bool inputRisingEdge = nowHigh && !wasHigh;
        prevTriggerValue = trigVal;

        // The manual-fire button drops a one-shot flag; consume it here so a single click fires
        // exactly one envelope even if DoCalc runs many times before/after the button press.
        bool manualFired = manualTriggerRequested;
        manualTriggerRequested = false;

        bool fireThisTick = inputRisingEdge || manualFired;

        if (fireThisTick)
        {
            // Restart from wherever we currently are, so a retrigger mid-release ramps smoothly
            // to 1 over `attack` rather than snapping to 0 first.
            phase = Phase.Attack;
            phaseTime = 0f;
            phaseStartValue = outValue;
        }

        // Time.deltaTime is per-frame; if the tick manager ever runs slower than one-tick-per-frame
        // this stays honest by measuring wall-clock delta between our own DoCalc calls.
        float now = Time.time;
        float dt = haveLastTick ? Mathf.Max(0f, now - lastTickTime) : 0f;
        lastTickTime = now;
        haveLastTick = true;
        phaseTime += dt;

        switch (phase)
        {
            case Phase.Idle:
                outValue = 0f;
                break;

            case Phase.Attack:
                if (AttackSec <= 0f)
                {
                    outValue = 1f;
                    EnterPhase(Phase.Decay, 1f);
                }
                else
                {
                    float t = Mathf.Clamp01(phaseTime / AttackSec);
                    outValue = Mathf.Lerp(phaseStartValue, 1f, t);
                    if (phaseTime >= AttackSec)
                    {
                        outValue = 1f;
                        EnterPhase(Phase.Decay, 1f);
                    }
                }
                break;

            case Phase.Decay:
                if (DecaySec <= 0f)
                {
                    outValue = sustain;
                    EnterPhase(Phase.Hold, sustain);
                }
                else
                {
                    float t = Mathf.Clamp01(phaseTime / DecaySec);
                    outValue = Mathf.Lerp(1f, sustain, t);
                    if (phaseTime >= DecaySec)
                    {
                        outValue = sustain;
                        EnterPhase(Phase.Hold, sustain);
                    }
                }
                break;

            case Phase.Hold:
                outValue = sustain;
                if (phaseTime >= HoldSec)
                    EnterPhase(Phase.Release, sustain);
                break;

            case Phase.Release:
                if (ReleaseSec <= 0f)
                {
                    outValue = 0f;
                    EnterPhase(Phase.Idle, 0f);
                }
                else
                {
                    float t = Mathf.Clamp01(phaseTime / ReleaseSec);
                    outValue = Mathf.Lerp(phaseStartValue, 0f, t);
                    if (phaseTime >= ReleaseSec)
                    {
                        outValue = 0f;
                        EnterPhase(Phase.Idle, 0f);
                    }
                }
                break;
        }

        valueKnob.SetValue<float>(outValue);
        gateKnob.SetValue<bool>(outValue > gateThreshold);
        onsetKnob.SetValue<bool>(fireThisTick);
        lastOnsetForSparkline = fireThisTick;
        return true;
    }

    private void EnterPhase(Phase next, float startValue)
    {
        phase = next;
        phaseTime = 0f;
        phaseStartValue = startValue;
    }

    // Renders the shape a single trigger would produce with the current parameters — a static
    // preview, distinct from the sparkline (which is a live trace of past values). Redraws only
    // on parameter change so we don't upload a fresh texture every repaint.
    private void DrawEnvelopePreview()
    {
        GUILayout.Space(4);
        GUILayout.Label("Shape");

        if (previewTex == null)
        {
            previewTex = new Texture2D(PreviewWidth, PreviewHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            prevPreviewAttackMs = float.NaN; // force a first draw
        }

        if (float.IsNaN(prevPreviewAttackMs)
            || !Mathf.Approximately(prevPreviewAttackMs, attackMs)
            || !Mathf.Approximately(prevPreviewDecayMs, decayMs)
            || !Mathf.Approximately(prevPreviewSustain, sustain)
            || !Mathf.Approximately(prevPreviewHoldMs, holdMs)
            || !Mathf.Approximately(prevPreviewReleaseMs, releaseMs))
        {
            RepaintPreview(previewTex);
            prevPreviewAttackMs = attackMs;
            prevPreviewDecayMs = decayMs;
            prevPreviewSustain = sustain;
            prevPreviewHoldMs = holdMs;
            prevPreviewReleaseMs = releaseMs;
        }

        GUILayout.Box(previewTex,
            GUILayout.Height(PreviewHeight),
            GUILayout.ExpandWidth(true));
    }

    // Paints the ADSR curve into `tex` as a solid connected line over dim reference lines.
    // Uses SampleEnvelopeShape so the visual is guaranteed to match what DoCalc produces.
    private void RepaintPreview(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        Color bg          = new Color(0.10f, 0.10f, 0.10f, 1f);
        Color zeroLine    = new Color(0.35f, 0.35f, 0.35f, 1f);
        Color sustainLine = new Color(0.28f, 0.28f, 0.28f, 1f);
        Color curveColor  = new Color(0.40f, 0.85f, 1.00f, 1f);
        Color compressedLine = new Color(0.55f, 0.45f, 0.20f, 1f); // marker on the sustain
                                                                    // line when hold is truncated

        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        // Baseline (y=0) so an all-zero release still reads as sitting on the floor.
        for (int x = 0; x < w; x++) pixels[x] = zeroLine;

        // Cap the displayed hold so a very long hold doesn't squish attack/decay/release into
        // invisible slivers. The cap floats with the surrounding phases so ADR-dominated
        // envelopes still get proportional hold, but a 10-second hold with 100ms phases isn't
        // allowed to steal 99% of the graph.
        float adrTotal = AttackSec + DecaySec + ReleaseSec;
        float displayHoldCap = Mathf.Max(2f, 3f * adrTotal);
        float displayHoldSec = Mathf.Min(HoldSec, displayHoldCap);
        bool holdCompressed = HoldSec > displayHoldSec + 0.001f;

        // Sustain-level reference line.
        int sustainY = Mathf.Clamp(Mathf.RoundToInt(sustain * (h - 1)), 0, h - 1);
        for (int x = 0; x < w; x++) pixels[sustainY * w + x] = sustainLine;

        // Total displayed duration in seconds; small floor so the shape renders as a spike
        // rather than collapsing to a single column when everything is zero.
        float totalDur = AttackSec + DecaySec + displayHoldSec + ReleaseSec;
        if (totalDur < 0.001f) totalDur = 0.001f;

        // Pixel span of the (visually-truncated) hold segment, for the compressed marker below.
        float holdStartT = AttackSec + DecaySec;
        float holdEndT   = holdStartT + displayHoldSec;
        int holdStartX = Mathf.Clamp(Mathf.RoundToInt(holdStartT / totalDur * (w - 1)), 0, w - 1);
        int holdEndX   = Mathf.Clamp(Mathf.RoundToInt(holdEndT   / totalDur * (w - 1)), 0, w - 1);

        int prevY = 0;
        for (int x = 0; x < w; x++)
        {
            float t = (x / (float)(w - 1)) * totalDur;
            float v = SampleEnvelopeShape(t, AttackSec, DecaySec, sustain, displayHoldSec, ReleaseSec);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (h - 1)), 0, h - 1);

            // Fill the vertical gap between adjacent samples so steep slopes read as a solid
            // line instead of a scatter of disconnected dots.
            if (x > 0)
            {
                int lo = Mathf.Min(prevY, y);
                int hi = Mathf.Max(prevY, y);
                for (int yy = lo; yy <= hi; yy++)
                    pixels[yy * w + x] = curveColor;
            }
            else
            {
                pixels[y * w + x] = curveColor;
            }
            prevY = y;
        }

        // Dashed-marker overlay on the sustain plateau when hold is compressed: makes it
        // visually obvious the on-screen span doesn't match the actual hold time.
        if (holdCompressed)
        {
            for (int x = holdStartX; x <= holdEndX; x++)
            {
                if ((x / 4) % 2 == 0) continue; // 4-pixel dashes
                int idx = sustainY * w + x;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = compressedLine;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false);
    }

    // Pure function of the ADSR parameters. Used by the preview so the rendered curve is
    // definitionally the same shape a trigger would play. Time argument and phase durations
    // are all in seconds; separated as parameters so the preview can substitute a visually-
    // compressed hold without touching the runtime envelope math.
    private float SampleEnvelopeShape(float t, float a, float d, float s, float ho, float r)
    {
        if (t < a)
            return a > 0f ? t / a : 1f;
        t -= a;
        if (t < d)
            return d > 0f ? Mathf.Lerp(1f, s, t / d) : s;
        t -= d;
        if (t < ho)
            return s;
        t -= ho;
        if (t < r)
            return r > 0f ? Mathf.Lerp(s, 0f, t / r) : 0f;
        return 0f;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (previewTex != null)
        {
            Object.DestroyImmediate(previewTex);
            previewTex = null;
        }
    }
}
