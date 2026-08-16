
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;



[Node(false, "MIDI/MIDIControlArrayMinis")]
public class MinisControlArrayNode : SignalNode
{
    public override string GetID => "MinisControlArrayNode";
    public override string Title { get { return "MinisControlArray"; } }
    private const int rescaleControlsHeight = 45;
    private const int nodeBaseHeight = 20;
    private const int controlBaseHeight = 60;

    private Vector2 _DefaultSize = new Vector2(200,
        nodeBaseHeight
        + 1 * controlBaseHeight);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    // One sparkline per bound control; knobs stay beside their control rows, so these
    // channels are texture+label only (no outputKnob)
    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        if (controls == null) yield break;
        foreach (var control in controls)
        {
            if (!control.bound) continue;
            var captured = control;
            yield return new SignalChannel
            {
                getValue = () => captured.outputKnob != null
                    ? captured.outputKnob.GetValue<float>()
                    : captured.rawMIDIValue,
                label    = $"cc{captured.controlID}",
            };
        }
    }

    [Serializable]
    public class BoundMidiControl
    {
        public float rawMIDIValue;
        public float rescaleMin = 0;
        public float rescaleMax = 1;
        public bool rescale = false;
        public int controlID;
        public bool bound = false;
        // Stable identity: names this control's ports and keys its MidiDeviceManager handler.
        // NEVER reuse across controls — index-based keys collide after unbind/rebind and
        // silently replace another control's handler (a bound CC "goes dead").
        public int uid = -1;

        // UnityEngine.Object references and transient state must not serialize: the canvas
        // working-copy clone does not remap references held inside nested classes, so a
        // serialized knob/parent points at the SOURCE canvas after every load. They are
        // re-derived in DoInit (HealKnobRefs / parent assignment) instead.
        [NonSerialized] public bool binding = false;
        [NonSerialized] public bool deleted = false;
        [NonSerialized] public Node parent;
        [NonSerialized] public ValueConnectionKnob minKnob;
        [NonSerialized] public ValueConnectionKnob maxKnob;
        [NonSerialized] public ValueConnectionKnob outputKnob;
        [NonSerialized] public Rect labelRect; // bind-label rect, for the hover device popup

        public string OutPortName => $"val{uid}";
        public string MinPortName => $"min{uid}";
        public string MaxPortName => $"max{uid}";

        // Parameterless constructor to please the XML serialization gods
        public BoundMidiControl() { }

        public BoundMidiControl(Node parent)
        {
            this.parent = parent;
        }

        public void AddOutputPort()
        {
            outputKnob = parent.CreateValueConnectionKnob(
                new ValueConnectionKnobAttribute(OutPortName, Direction.Out, typeof(float), NodeSide.Right));
        }

        public void SetRescalePorts()
        {
            if (rescale)
            {
                AddRescalePorts();
            }
            else
            {
                RemoveRescalePorts();
            }
        }

        public void AddRescalePorts()
        {
            minKnob = parent.CreateValueConnectionKnob(
                new ValueConnectionKnobAttribute(MinPortName, Direction.In, typeof(float), NodeSide.Left));
            maxKnob = parent.CreateValueConnectionKnob(
                new ValueConnectionKnobAttribute(MaxPortName, Direction.In, typeof(float), NodeSide.Left));
        }

        public void OnDelete()
        {
            RemoveRescalePorts();
            if (outputKnob != null)
            {
                outputKnob.ClearConnections();
                parent.DeleteConnectionPort(outputKnob);
                outputKnob = null;
            }
        }

        public void RemoveRescalePorts()
        {
            if (minKnob != null)
            {
                minKnob.ClearConnections();
                parent.DeleteConnectionPort(minKnob);
                minKnob = null;
            }
            if (maxKnob != null)
            {
                maxKnob.ClearConnections();
                parent.DeleteConnectionPort(maxKnob);
                maxKnob = null;
            }
        }
    }

    public int channel;
    public List<BoundMidiControl> controls;
    public int nextControlUid = 0;
    // Captured at bind time so the physical-device layout resolves exactly (see MinisControlNode)
    public string deviceProduct = "";

    public int numControls => controls.Count;

    private string nodeInstanceId;
    private int bindingIndex = 0;

    [NonSerialized] private RenderTexture layoutTex;
    [NonSerialized] private BoundMidiControl hoveredControl;
    [NonSerialized] private int lastLayoutRenderFrame = -1;

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();

        if (controls == null)
        {
            controls = new List<BoundMidiControl>();
        }
        foreach (var control in controls)
        {
            control.parent = this; // serialized parents point at the pre-clone node
            if (control.uid < 0)
            {
                control.uid = nextControlUid++; // legacy saves predate stable uids
            }
        }
        // Always keep one open slot at the bottom to bind the next control into
        if (controls.Count == 0 || controls[controls.Count - 1].bound)
        {
            controls.Add(new BoundMidiControl(this) { uid = nextControlUid++ });
        }

        HealKnobRefs();

        // Register any already-bound controls with MidiDeviceManager
        foreach (var control in controls)
        {
            if (control.bound)
            {
                RegisterControl(control);
            }
        }
        SetSize();
    }

    /// <summary>
    /// Re-derives every control's knob references from the serialized dynamic ports.
    /// New saves match by unique port name; legacy saves (ports all named "Value"/
    /// "rescaleMin"/"rescaleMax") fall back to name+direction order matching, then get
    /// renamed to the unique scheme so future loads take the exact path. Leftover ports
    /// that no control claims are orphans from pre-fix bugs and are deleted.
    /// </summary>
    private void HealKnobRefs()
    {
        var unclaimed = dynamicConnectionPorts.Cast<ValueConnectionKnob>().ToList();

        ValueConnectionKnob TakeByName(string name)
        {
            for (int i = 0; i < unclaimed.Count; i++)
            {
                if (unclaimed[i].name == name)
                {
                    var knob = unclaimed[i];
                    unclaimed.RemoveAt(i);
                    return knob;
                }
            }
            return null;
        }
        ValueConnectionKnob TakeFirstLegacy(string legacyName, Direction direction)
        {
            for (int i = 0; i < unclaimed.Count; i++)
            {
                if (unclaimed[i].name == legacyName && unclaimed[i].direction == direction)
                {
                    var knob = unclaimed[i];
                    unclaimed.RemoveAt(i);
                    return knob;
                }
            }
            return null;
        }

        // Pass 1: exact unique names. Pass 2: legacy names in serialized-order (outputs and
        // rescale pairs matched independently, so interleaved creation order still maps right).
        foreach (var control in controls)
        {
            if (!control.bound) continue;
            control.outputKnob = TakeByName(control.OutPortName);
            if (control.outputKnob == null) control.outputKnob = TakeFirstLegacy("Value", Direction.Out);
            if (control.rescale)
            {
                control.minKnob = TakeByName(control.MinPortName);
                if (control.minKnob == null) control.minKnob = TakeFirstLegacy("rescaleMin", Direction.In);
                control.maxKnob = TakeByName(control.MaxPortName);
                if (control.maxKnob == null) control.maxKnob = TakeFirstLegacy("rescaleMax", Direction.In);
            }
            // Migrate legacy names to the unique scheme (persisted with the canvas)
            if (control.outputKnob != null) control.outputKnob.name = control.OutPortName;
            if (control.minKnob != null) control.minKnob.name = control.MinPortName;
            if (control.maxKnob != null) control.maxKnob.name = control.MaxPortName;

            // A bound control missing its output port (pre-fix data loss) gets a fresh one
            if (control.outputKnob == null) control.AddOutputPort();
            if (control.rescale && (control.minKnob == null || control.maxKnob == null))
            {
                control.RemoveRescalePorts();
                control.AddRescalePorts();
            }
        }

        foreach (var orphan in unclaimed)
        {
            Debug.LogWarning($"[MinisControlArray] Deleting orphaned port '{orphan.name}' left by an older version of this node.");
            orphan.ClearConnections();
            DeleteConnectionPort(orphan);
        }
    }

    private string ControlKey(BoundMidiControl control) => $"{nodeInstanceId}_uid{control.uid}";

    private void RegisterControl(BoundMidiControl control)
    {
        MidiDeviceManager.Instance.RegisterControlHandler(ControlKey(control), channel, control.controlID,
            (cc, value) => ReceiveMIDIMessageForControl(control, cc, value));
    }

    public override void OnDestroy()
    {
        // Unregister all controls from MidiDeviceManager
        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.UnregisterNode(nodeInstanceId);
        }
        if (layoutTex != null)
        {
            layoutTex.Release();
            layoutTex = null;
        }
        base.OnDestroy(); // releases sparkline GPU resources
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    void BeginBindingMinis()
    {
        controls[bindingIndex].binding = true;
        MidiDeviceManager.Instance.BeginControlBinding(nodeInstanceId, OnBindComplete);
    }

    private void OnBindComplete(Minis.MidiDevice device, int deviceChannel, int deviceControlID)
    {
        if (bindingIndex < 0 || bindingIndex >= controls.Count) return;
        var control = controls[bindingIndex];
        channel = deviceChannel;
        deviceProduct = device != null ? (device.description.product ?? "") : "";
        control.controlID = deviceControlID;
        control.binding = false;
        control.bound = true;
        control.AddOutputPort();

        RegisterControl(control);

        // Add new empty control slot
        controls.Add(new BoundMidiControl(this) { uid = nextControlUid++ });
        SetSize();
    }

    void ReceiveMIDIMessageForControl(BoundMidiControl control, Minis.MidiValueControl cc, float value)
    {
        if (cc.controlNumber == control.controlID)
        {
            control.rawMIDIValue = value;
        }
    }

    private void SetSize()
    {
        _DefaultSize = new Vector2(160,
             nodeBaseHeight
            + numControls * controlBaseHeight
            + controls.Where(cc => cc.rescale).Sum(i => rescaleControlsHeight)
        );
    }

    public override void NodeGUI()
    {
        // Structural changes (ports added/removed, controls deleted) are deferred to after
        // the draw loop: mutating mid-pass desyncs this pass's Layout from its Repaint and
        // flickers the rest of the canvas.
        BoundMidiControl unbindRequested = null;
        BoundMidiControl rescaleToggled = null;
        int channelIdx = 0; // sparkline channel index, tracking GetSignalChannels order

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        DrawSparklineToggle();
        foreach (var control in controls)
        {
            if (!control.bound && !control.binding)
            {
                if (GUILayout.Button("Bind input knob"))
                {
                    bindingIndex = controls.IndexOf(control);
                    BeginBindingMinis();
                }
            }
            else
            {
                if (control.bound)
                {
                    GUILayout.BeginHorizontal();
                    string label = string.Format(" {0} ctrl {1}: {2:0.00}", channel.ToString(), control.controlID, control.rawMIDIValue);
                    GUIContent content = new GUIContent(label);
                    if (control.outputKnob != null)
                    {
                        control.outputKnob.DisplayLayout(content);
                    }
                    else
                    {
                        GUILayout.Label(content);
                    }
                    // capture the label rect for the hover device popup (rect valid on Repaint;
                    // hover itself is resolved during Layout to keep GUI structure consistent)
                    if (Event.current.type == EventType.Repaint)
                    {
                        control.labelRect = GUILayoutUtility.GetLastRect();
                    }
                    if (GUILayout.Button("Unbind"))
                    {
                        unbindRequested = control;
                    }
                    GUILayout.EndHorizontal();
                    // Rescale float inputs: draw per the CURRENT rescale state, apply the
                    // toggle after the loop
                    bool newRescale = RTEditorGUI.Toggle(control.rescale, "Rescale value");
                    if (newRescale != control.rescale)
                    {
                        rescaleToggled = control;
                    }
                    if (control.rescale)
                    {
                        FloatKnobOrField(GUIContent.none, ref control.rescaleMin, control.minKnob);
                        FloatKnobOrField(GUIContent.none, ref control.rescaleMax, control.maxKnob);
                    }
                    // this control's sparkline, inline so the trace sits beside its CC
                    DrawSparklineChannel(channelIdx);
                    channelIdx++;
                }
                else
                {
                    GUILayout.Label("Use control to bind");
                }
            }
            // Horizontal rule
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("─────────────────");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        // Hover state drives the popup's GUI structure, so it only mutates during Layout
        // (mid-Repaint flips desync the layout cursor and flicker the canvas)
        if (Event.current.type == EventType.Layout)
        {
            hoveredControl = null;
            foreach (var control in controls)
            {
                if (control.bound && control.labelRect.Contains(Event.current.mousePosition))
                {
                    hoveredControl = control;
                    break;
                }
            }
        }

        DrawDeviceView();

        if (unbindRequested != null)
        {
            MidiDeviceManager.Instance.UnregisterControlHandler(ControlKey(unbindRequested), channel, unbindRequested.controlID);
            unbindRequested.controlID = 0;
            unbindRequested.bound = false;
            unbindRequested.deleted = true;
            unbindRequested.OnDelete();
            controls.RemoveAll(cc => cc.deleted);
            SetSize();
        }
        if (rescaleToggled != null)
        {
            rescaleToggled.rescale = !rescaleToggled.rescale;
            rescaleToggled.SetRescalePorts();
            SetSize();
        }

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    // Device picture with the hovered control's CC bulls-eyed. Same conventions as
    // MinisControlNode: hint is always present (structure never changes with hover),
    // popup appears while a bound control's label is hovered.
    private void DrawDeviceView()
    {
        if (!Application.isPlaying || MidiDeviceManager.Instance == null) return;
        var layout = MidiDeviceManager.Instance.GetLayoutFor(deviceProduct, channel);
        if (layout == null) return;
        bool anyBound = false;
        foreach (var control in controls)
        {
            if (control.bound) { anyBound = true; break; }
        }
        if (!anyBound) return;
        GUILayout.Label("Hover a binding label for physical location", HintStyle);
        if (hoveredControl != null && layout.ContainsCcId(hoveredControl.controlID) && layoutTex != null)
        {
            GUILayout.Box(layoutTex, GUILayout.Width(MidiLayoutRenderer.TexWidth), GUILayout.Height(MidiLayoutRenderer.TexHeight));
        }
    }

    static GUIStyle _hintStyle;
    static GUIStyle HintStyle
    {
        get
        {
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.45f) },
                };
            }
            return _hintStyle;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHintStyle()
    {
        _hintStyle = null;
    }

    public override bool DoCalc()
    {
        // Re-render the device picture while a control is hovered (bulls-eye pulse animates);
        // frame-guarded so GPU work happens once per frame, in the Update phase
        if (hoveredControl != null && hoveredControl.bound && Application.isPlaying
            && Time.frameCount != lastLayoutRenderFrame)
        {
            var layout = MidiDeviceManager.Instance != null
                ? MidiDeviceManager.Instance.GetLayoutFor(deviceProduct, channel)
                : null;
            if (layout != null && layout.ContainsCcId(hoveredControl.controlID))
            {
                if (layoutTex == null) layoutTex = MidiLayoutRenderer.CreateTexture();
                MidiLayoutRenderer.Render(layout, hoveredControl.controlID, layoutTex, Time.time);
                lastLayoutRenderFrame = Time.frameCount;
            }
        }
        foreach (var control in controls)
        {
            if (!control.bound || control.outputKnob == null) continue;
            if (control.rescale && control.minKnob != null && control.maxKnob != null)
            {
                if (control.minKnob.connected())
                {
                    control.rescaleMin = control.minKnob.GetValue<float>();
                }
                if (control.maxKnob.connected())
                {
                    control.rescaleMax = control.maxKnob.GetValue<float>();
                }
            }
            float val = control.rawMIDIValue;
            if (control.rescale)
            {
                val = Mathf.Lerp(control.rescaleMin, control.rescaleMax, control.rawMIDIValue);
            }
            control.outputKnob.SetValue(val);
        }
        return true;
    }
}
