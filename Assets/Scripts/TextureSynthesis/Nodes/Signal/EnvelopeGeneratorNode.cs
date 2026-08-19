using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;
using SecretFire.TextureSynth.Timeline;

using System.Collections.Generic;

using UnityEngine;

// Gated envelope with a freely editable curve (Timeline's curve machinery: drag keys,
// double-click to add, right-click to delete, tangent handles on the selected key).
//
// Trigger semantics: bool input. A rising edge starts the curve from t=0 (blending from the
// current output over a few ms so retriggers don't click). If the gate is STILL HELD when
// playback reaches the sustain marker (orange line, draggable), the envelope parks at the
// curve's value there until the gate releases, then plays the remainder of the curve as the
// release tail. One-tick pulses (Timeline events, onset chains) sail straight through the
// marker — both gate-held and pulse usage work with no mode switch.
//
// The envelope's total length is an explicit duration (KnobOrField, so it can be driven by
// a signal). Changing duration rescales keys, sustain marker, and a running playhead
// proportionally — the shape stretches, TimelineNode-style. Idle output is the curve's
// final value (usually 0).
//
// Outputs: value (the shaped float), gate (bool, value > threshold), onset (bool, one tick
// per trigger — wire THIS to event-semantics consumers, not `gate`).
[Node(false, "Signal/EnvelopeGenerator")]
public class EnvelopeGeneratorNode : SignalNode
{
    public override string GetID => "EnvelopeGeneratorNode";
    public override string Title { get { return "EnvelopeGenerator"; } }

    private Vector2 _DefaultSize = new Vector2(260, 300);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("trigger", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob triggerKnob;

    [ValueConnectionKnob("duration", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob durationKnob;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    [ValueConnectionKnob("gate", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob gateKnob;

    [ValueConnectionKnob("onset", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob onsetKnob;

    public TimelineCurve curve = new TimelineCurve();
    public float sustainTime = 0.4f;
    public float gateThreshold = 0.05f;
    public float duration = 2f;

    const float MinDuration = 0.05f;
    const float RetriggerBlendSec = 0.03f;
    const float CurveEditorHeight = 90f;
    const float KeyHitRadius = 8f;
    const float TangentHandleLength = 28f;

    static readonly Color EditorBg = new Color(0.10f, 0.10f, 0.10f, 1f);
    static readonly Color GridColor = new Color(1f, 1f, 1f, 0.08f);
    static readonly Color CurveColor = new Color(0.40f, 0.85f, 1.00f, 1f);
    static readonly Color KeyColor = new Color(1.00f, 0.95f, 0.40f, 1f);
    static readonly Color KeySelected = new Color(1.00f, 0.55f, 0.30f, 1f);
    static readonly Color SustainColor = new Color(1.00f, 0.60f, 0.20f, 0.9f);
    static readonly Color PlayheadColor = new Color(1.00f, 0.30f, 0.25f, 0.9f);

    // ---- runtime state ----
    enum DragKind { None, Key, TangentIn, TangentOut, Sustain }
    [System.NonSerialized] private bool active;
    [System.NonSerialized] private bool holdingAtSustain;
    [System.NonSerialized] private float t;
    [System.NonSerialized] private float outValue;
    [System.NonSerialized] private bool prevGate;
    [System.NonSerialized] private float blendFrom;
    [System.NonSerialized] private float blendTime = 1f;
    [System.NonSerialized] private float lastTickTime;
    [System.NonSerialized] private bool haveLastTick;
    [System.NonSerialized] private bool manualTriggerRequested;
    [System.NonSerialized] private bool lastOnsetForSparkline;
    [System.NonSerialized] private Rect curveRect;
    [System.NonSerialized] private int selKey = -1;
    [System.NonSerialized] private DragKind drag = DragKind.None;
    [System.NonSerialized] private int dragKey = -1;
    [System.NonSerialized] private Vector2[] sampleBuf;

    float EnvEnd => Mathf.Max(duration, MinDuration);

    public override void DoInit()
    {
        if (curve == null) curve = new TimelineCurve();
        curve.EnsureValid();
        if (curve.KeyCount < 2)
        {
            // ADSR-flavored starter shape; entirely user-editable from here
            curve.keys.Clear();
            curve.AddKey(0f, 0f);
            curve.AddKey(0.12f, 1f);
            curve.AddKey(0.4f, 0.6f);
            curve.AddKey(2f, 0f);
        }
        // Saves that predate the explicit duration field derived length from the last key
        float lastKeyTime = curve.KeyCount > 0 ? curve.GetKey(curve.KeyCount - 1).time : 2f;
        duration = Mathf.Max(duration, lastKeyTime, MinDuration);
        sustainTime = Mathf.Clamp(sustainTime, 0f, EnvEnd);
    }

    // Duration changes stretch the whole envelope proportionally: keys, sustain marker,
    // and a running playhead all keep their relative positions (TimelineNode precedent).
    void SetDuration(float newDur)
    {
        newDur = Mathf.Max(newDur, MinDuration);
        if (Mathf.Abs(newDur - duration) < 1e-4f) return;
        float factor = newDur / EnvEnd;
        curve.ScaleTimes(factor);
        sustainTime *= factor;
        if (active) t *= factor;
        duration = newDur;
    }

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
        yield return new SignalChannel
        {
            outputKnob = onsetKnob,
            getValue = () => lastOnsetForSparkline ? 1f : 0f,
            label = "onset",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        triggerKnob.DisplayLayout();
        if (GUILayout.Button("Trigger", GUILayout.Width(70)))
            manualTriggerRequested = true;
        GUILayout.FlexibleSpace();
        GUILayout.Label(active ? (holdingAtSustain ? "Sustain" : "Playing") : "Idle", GUILayout.Width(60));
        GUILayout.EndHorizontal();

        // Curve editor: reserve layout space, then draw/interact in fixed rects within it
        Rect r = GUILayoutUtility.GetRect(10f, CurveEditorHeight, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            curveRect = r;
        HandleCurveInput();
        if (Event.current.type == EventType.Repaint)
            DrawCurveEditor(r);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"sustain @ {sustainTime:0.00}s", GUILayout.Width(100));
        float newDur = duration;
        FloatKnobOrField("duration", ref newDur, durationKnob);
        GUILayout.EndHorizontal();
        if (newDur != duration) SetDuration(newDur);

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    // ---------------------------------------------------------------- curve editor

    float TimeToX(float time) => curveRect.x + (time / EnvEnd) * curveRect.width;
    float XToTime(float x) => Mathf.Clamp((x - curveRect.x) / Mathf.Max(curveRect.width, 1f), 0f, 1f) * EnvEnd;
    float ValueToY(float v) => curveRect.yMax - Mathf.Clamp01(v) * curveRect.height;
    float YToValue(float y) => Mathf.Clamp01((curveRect.yMax - y) / Mathf.Max(curveRect.height, 1f));

    void DrawCurveEditor(Rect r)
    {
        FillRect(r, EditorBg);
        for (int i = 1; i < 4; i++)
        {
            FillRect(new Rect(r.x + r.width * i / 4f, r.y, 1f, r.height), GridColor);
            FillRect(new Rect(r.x, r.y + r.height * i / 4f, r.width, 1f), GridColor);
        }

        // sustain marker
        float sx = TimeToX(sustainTime);
        FillRect(new Rect(sx - 1f, r.y, 2f, r.height), SustainColor);
        FillRect(new Rect(sx - 4f, r.y, 9f, 5f), SustainColor);

        // curve polyline
        int samples = Mathf.Max(2, (int)(r.width / 2f) + 1);
        if (sampleBuf == null || sampleBuf.Length != samples)
            sampleBuf = new Vector2[samples];
        for (int s = 0; s < samples; s++)
        {
            float time = (s / (float)(samples - 1)) * EnvEnd;
            sampleBuf[s] = new Vector2(r.x + (r.width * s) / (samples - 1), ValueToY(curve.Evaluate(time)));
        }
        RTEditorGUI.DrawPolygonLine(sampleBuf, CurveColor, Texture2D.whiteTexture, 2f);

        // keys + selected tangents
        for (int k = 0; k < curve.KeyCount; k++)
        {
            var key = curve.GetKey(k);
            var p = new Vector2(TimeToX(key.time), ValueToY(key.value));
            bool isSel = k == selKey;
            if (isSel)
            {
                DrawTangentHandle(key, p, true);
                DrawTangentHandle(key, p, false);
            }
            FillRect(new Rect(p.x - 3f, p.y - 3f, 7f, 7f), isSel ? KeySelected : KeyColor);
        }

        // playhead while the envelope runs
        if (active)
        {
            float px = TimeToX(holdingAtSustain ? sustainTime : t);
            FillRect(new Rect(px, r.y, 1f, r.height), PlayheadColor);
        }
    }

    Vector2 TangentHandlePos(CurveKey key, Vector2 keyPx, bool inHandle)
    {
        float pxPerSec = curveRect.width / EnvEnd;
        float pxPerVal = curveRect.height;
        float slope = inHandle ? key.inTangent : key.outTangent;
        var dir = new Vector2(1f, -slope * pxPerVal / pxPerSec).normalized * TangentHandleLength;
        return inHandle ? keyPx - dir : keyPx + dir;
    }

    void DrawTangentHandle(CurveKey key, Vector2 keyPx, bool inHandle)
    {
        Vector2 hp = TangentHandlePos(key, keyPx, inHandle);
        RTEditorGUI.DrawLine(keyPx, hp, new Color(1f, 1f, 1f, 0.5f), Texture2D.whiteTexture, 1f);
        FillRect(new Rect(hp.x - 2f, hp.y - 2f, 5f, 5f), Color.white);
    }

    int KeyAt(Vector2 guiPos)
    {
        for (int k = 0; k < curve.KeyCount; k++)
        {
            var key = curve.GetKey(k);
            var p = new Vector2(TimeToX(key.time), ValueToY(key.value));
            if ((p - guiPos).magnitude <= KeyHitRadius) return k;
        }
        return -1;
    }

    void HandleCurveInput()
    {
        Event e = Event.current;
        if (e == null || curveRect.width <= 0f) return;
        Vector2 m = e.mousePosition;

        // A MouseUp consumed by another handler (type == Used) or released outside the
        // window (type == Ignore) never matches the MouseUp case below; rawType still
        // says MouseUp. Without this, the drag pseudo-capture sticks across clicks.
        if (drag != DragKind.None && e.rawType == EventType.MouseUp && e.type != EventType.MouseUp)
        {
            drag = DragKind.None;
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button != 0 || !curveRect.Contains(m)) break;
                // selected key's tangent handles first
                if (selKey >= 0 && selKey < curve.KeyCount)
                {
                    var sel = curve.GetKey(selKey);
                    var keyPx = new Vector2(TimeToX(sel.time), ValueToY(sel.value));
                    if ((TangentHandlePos(sel, keyPx, true) - m).magnitude <= KeyHitRadius)
                    {
                        drag = DragKind.TangentIn;
                        dragKey = selKey;
                        e.Use();
                        break;
                    }
                    if ((TangentHandlePos(sel, keyPx, false) - m).magnitude <= KeyHitRadius)
                    {
                        drag = DragKind.TangentOut;
                        dragKey = selKey;
                        e.Use();
                        break;
                    }
                }
                int hit = KeyAt(m);
                if (hit >= 0)
                {
                    selKey = hit;
                    drag = DragKind.Key;
                    dragKey = hit;
                }
                else if (Mathf.Abs(TimeToX(sustainTime) - m.x) <= 5f)
                {
                    drag = DragKind.Sustain;
                }
                else if (e.clickCount >= 2)
                {
                    selKey = curve.AddKey(XToTime(m.x), YToValue(m.y));
                    drag = DragKind.Key;
                    dragKey = selKey;
                }
                else
                {
                    selKey = -1;
                }
                e.Use();
                break;

            case EventType.MouseDrag:
                if (drag == DragKind.None) break;
                switch (drag)
                {
                    case DragKind.Sustain:
                        sustainTime = Mathf.Clamp(XToTime(m.x), 0f, EnvEnd);
                        break;
                    case DragKind.Key:
                        if (dragKey >= 0 && dragKey < curve.KeyCount)
                        {
                            // XToTime clamps to [0, duration]: keys live inside the fixed axis
                            dragKey = curve.MoveKey(dragKey, XToTime(m.x), YToValue(m.y));
                            selKey = dragKey;
                        }
                        break;
                    case DragKind.TangentIn:
                    case DragKind.TangentOut:
                        if (dragKey >= 0 && dragKey < curve.KeyCount)
                        {
                            var key = curve.GetKey(dragKey);
                            var keyPx = new Vector2(TimeToX(key.time), ValueToY(key.value));
                            float dxp = m.x - keyPx.x;
                            dxp = drag == DragKind.TangentIn ? Mathf.Min(dxp, -1f) : Mathf.Max(dxp, 1f);
                            float dyp = m.y - keyPx.y;
                            float pxPerSec = curveRect.width / EnvEnd;
                            float pxPerVal = curveRect.height;
                            curve.SetLinkedTangent(dragKey, -(dyp / dxp) * pxPerSec / pxPerVal);
                        }
                        break;
                }
                e.Use();
                break;

            case EventType.MouseUp:
                if (drag != DragKind.None)
                {
                    drag = DragKind.None;
                    e.Use();
                }
                break;
        }
    }

    // Right-click key delete must pre-empt the framework's context menu (MouseDown @0 consumes
    // button 1 before NodeGUI) — same pattern as TimelineNode's handler; self-scoped, so both
    // handlers coexist at this priority.
    [EventHandlerAttribute(EventType.MouseDown, -1)]
    static void HandleEnvelopeRightClick(NodeEditorInputInfo info)
    {
        if (info.inputEvent.button != 1) return;
        var state = info.editorState;
        if (state == null || !(state.focusedNode is EnvelopeGeneratorNode env)) return;
        Vector2 local = NodeEditor.ScreenToCanvasSpace(state, info.inputPos)
                      - env.rect.position - new Vector2(0f, 20f); // header offset
        if (!env.curveRect.Contains(local)) return;
        int k = env.KeyAt(local);
        if (k >= 0 && env.curve.KeyCount > 2)
        {
            env.curve.RemoveKey(k);
            env.selKey = -1;
            info.inputEvent.Use();
            NodeEditor.RepaintClients();
        }
    }

    static void FillRect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // ---------------------------------------------------------------- calc

    public override bool DoCalc()
    {
        // Knob-driven duration must apply even when the canvas GUI isn't drawing
        if (durationKnob != null && durationKnob.connected())
            SetDuration(durationKnob.GetValue<float>());

        bool gateHeld = triggerKnob != null && triggerKnob.connected() && triggerKnob.GetValue<bool>();
        bool rising = gateHeld && !prevGate;
        prevGate = gateHeld;

        bool manualFired = manualTriggerRequested;
        manualTriggerRequested = false;
        bool fireThisTick = rising || manualFired;

        // Wall-clock delta between our own ticks, honest even if the tick manager skips frames
        float now = Time.time;
        float dt = haveLastTick ? Mathf.Max(0f, now - lastTickTime) : 0f;
        lastTickTime = now;
        haveLastTick = true;

        if (fireThisTick)
        {
            active = true;
            holdingAtSustain = false;
            blendFrom = outValue;
            blendTime = 0f;
            t = 0f;
        }

        float end = EnvEnd;
        if (active)
        {
            if (holdingAtSustain)
            {
                if (!gateHeld) holdingAtSustain = false; // released: fall through to the tail next tick
                outValue = curve.Evaluate(sustainTime);
            }
            else
            {
                float prevT = t;
                t += dt;
                if (gateHeld && prevT < sustainTime && t >= sustainTime)
                {
                    // gate still held when we reach the marker: park here until release
                    t = sustainTime;
                    holdingAtSustain = true;
                }
                if (t >= end)
                {
                    t = end;
                    active = false;
                }
                outValue = curve.Evaluate(t);
            }
            // short blend from the pre-trigger value so retriggers don't click
            blendTime += dt;
            float blend = Mathf.Clamp01(blendTime / RetriggerBlendSec);
            outValue = Mathf.Lerp(blendFrom, outValue, blend);
        }
        else
        {
            outValue = curve.Evaluate(end);
        }
        // Same clamp the editor renders with: hermite overshoot between keys can leave [0,1]
        outValue = Mathf.Clamp01(outValue);

        valueKnob.SetValue<float>(outValue);
        gateKnob.SetValue<bool>(outValue > gateThreshold);
        onsetKnob.SetValue<bool>(fireThisTick);
        lastOnsetForSparkline = fireThisTick;
        return true;
    }
}
