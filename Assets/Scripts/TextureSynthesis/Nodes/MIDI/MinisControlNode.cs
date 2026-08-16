
using Minis;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;



[Node(false, "MIDI/MIDIControlMinis")]
public class MinisControlNode : SignalNode
{
    public override string GetID => "MinisControlNode";
    public override string Title { get { return "MinisControl"; } }


    private Vector2 _DefaultSize = new Vector2(150, 85);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    bool binding = false;
    public bool bound = false;

    [ValueConnectionKnob("value", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob valueKnob;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = valueKnob,
            getValue   = () => valueKnob.GetValue<float>(),
            label      = "Value",
        };
    }

    public float rawMIDIValue;
    public float rescaleMin = 0;
    public float rescaleMax = 1;
    public bool rescale = false;
    public int controlID;

    public int channel;
    // Captured at bind time so the physical-device layout resolves exactly, even with
    // multiple MIDI devices connected or after a canvas reload. Empty on legacy saves.
    public string deviceProduct = "";
    private string nodeInstanceId;

    [System.NonSerialized] private RenderTexture layoutTex;
    [System.NonSerialized] private bool labelHovered = false;
    [System.NonSerialized] private Rect labelRect;
    [System.NonSerialized] private int lastLayoutRenderFrame = -1;

    private void SetSize()
    {
        _DefaultSize = new Vector2(150, rescale ? 125 : 85);
    }

    public override void DoInit()
    {
        nodeInstanceId = GetInstanceID().ToString();
        SetSize();

        // If already bound, register with MidiDeviceManager
        if (bound)
        {
            MidiDeviceManager.Instance.RegisterControlHandler(nodeInstanceId, channel, controlID, ReceiveMIDIMessage);
        }
    }

    public override void OnDestroy()
    {
        // Unregister from MidiDeviceManager
        if (MidiDeviceManager.Instance != null)
        {
            MidiDeviceManager.Instance.UnregisterNode(nodeInstanceId);
        }
        if (layoutTex != null)
        {
            layoutTex.Release();
            layoutTex = null;
        }
        base.OnDestroy();
    }

    private void OnDisable()
    {
        OnDestroy();
    }

    void SetRescalePorts()
    {
        if (rescale)
        {
            ValueConnectionKnobAttribute minKnobAttrib = new ValueConnectionKnobAttribute("rescaleMin", Direction.In, typeof(float), NodeSide.Left);
            ValueConnectionKnobAttribute maxKnobAttrib = new ValueConnectionKnobAttribute("rescaleMax", Direction.In, typeof(float), NodeSide.Left);
            CreateValueConnectionKnob(minKnobAttrib);
            CreateValueConnectionKnob(maxKnobAttrib);
        } 
        else
        {
            DeleteConnectionPort(dynamicConnectionPorts[1]);
            DeleteConnectionPort(dynamicConnectionPorts[0]);
        }
        SetSize();
    }

    void BeginBindingMinis()
    {
        binding = true;
        MidiDeviceManager.Instance.BeginControlBinding(nodeInstanceId, OnBindComplete);
    }

    private void OnBindComplete(Minis.MidiDevice device, int deviceChannel, int deviceControlID)
    {
        channel = deviceChannel;
        controlID = deviceControlID;
        deviceProduct = device != null ? (device.description.product ?? "") : "";
        binding = false;
        bound = true;

        // Register handler with MidiDeviceManager
        MidiDeviceManager.Instance.RegisterControlHandler(nodeInstanceId, channel, controlID, ReceiveMIDIMessage);
    }

    void ReceiveMIDIMessage(Minis.MidiValueControl cc, float value)
    {
        if (cc.controlNumber == controlID)
        {
            rawMIDIValue = value;
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind input knob"))
            {
                BeginBindingMinis();
            }
        }
        else
        {
            if (bound)
            {
                string label = string.Format("{0} ctrl {1}: {2:0.00}", channel.ToString(), controlID, rawMIDIValue);
                // Fixed width so the hover region doesn't jitter as the value's digits change
                GUILayout.Label(label, GUILayout.Width(150));
                // Hovering the binding label pops up the device picture. The rect is only
                // valid during Repaint, but hover state drives GUI STRUCTURE (the popup box),
                // so it must only change during Layout — flipping it mid-Repaint draws
                // elements the Layout pass never allocated, which desyncs the GUILayout
                // cursor and makes later GUI (menu bar, other nodes) flicker black.
                if (Event.current.type == EventType.Repaint)
                {
                    labelRect = GUILayoutUtility.GetLastRect();
                }
                else if (Event.current.type == EventType.Layout)
                {
                    labelHovered = labelRect.Contains(Event.current.mousePosition);
                }
                if (GUILayout.Button("Unbind"))
                {
                    MidiDeviceManager.Instance.UnregisterControlHandler(nodeInstanceId, channel, controlID);
                    controlID = 0;
                    bound = false;
                    deviceProduct = "";
                    labelHovered = false;
                }
            }
            else
            {
                GUILayout.Label("Use control to bind");
            }
        }

        // Rescale float inputs
        bool lastRescale = rescale;
        rescale = RTEditorGUI.Toggle(rescale, "Rescale value");
        if (lastRescale != rescale)
        {
            SetRescalePorts();
        }
        if (rescale && dynamicConnectionPorts.Count >= 2)
        {
            FloatKnobOrField(GUIContent.none, ref rescaleMin, (ValueConnectionKnob)dynamicConnectionPorts[0]);
            FloatKnobOrField(GUIContent.none, ref rescaleMax, (ValueConnectionKnob)dynamicConnectionPorts[1]);
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        DrawSparkline();
        DrawDeviceView();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
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

    // Pops up the device picture (bound control bulls-eyed) while the binding label is
    // hovered. Only shown when the bound device has a known layout (currently the MIDIMix).
    private void DrawDeviceView()
    {
        if (!bound || !Application.isPlaying || MidiDeviceManager.Instance == null) return;
        var layout = MidiDeviceManager.Instance.GetLayoutFor(deviceProduct, channel);
        if (layout == null || !layout.ContainsCcId(controlID)) return;
        // Always-present hint keeps the feature discoverable; its structure never changes
        // with hover, so it can't desync the GUILayout passes
        GUILayout.Label("Hover binding label for physical location", HintStyle);
        if (labelHovered && layoutTex != null)
        {
            GUILayout.Box(layoutTex, GUILayout.Width(MidiLayoutRenderer.TexWidth), GUILayout.Height(MidiLayoutRenderer.TexHeight));
        }
    }

    public override bool DoCalc()
    {
        // The bulls-eye pulse animates, so re-render each frame while the popup is visible —
        // but ONLY on the first DoCalc of the frame. Calculate can be re-invoked mid-OnGUI
        // (OnNodeChange recalcs), and dispatching into the texture while the GUI is drawing
        // it makes the popup image flicker; the frame guard keeps all GPU work in Update.
        if (bound && labelHovered && Application.isPlaying && Time.frameCount != lastLayoutRenderFrame)
        {
            var layout = MidiDeviceManager.Instance != null
                ? MidiDeviceManager.Instance.GetLayoutFor(deviceProduct, channel)
                : null;
            if (layout != null && layout.ContainsCcId(controlID))
            {
                if (layoutTex == null) layoutTex = MidiLayoutRenderer.CreateTexture();
                MidiLayoutRenderer.Render(layout, controlID, layoutTex, Time.time);
                lastLayoutRenderFrame = Time.frameCount;
            }
        }
        if (rescale && dynamicConnectionPorts.Count >= 2)
        {
            if (((ValueConnectionKnob)dynamicConnectionPorts[0]).connected())
            {
                rescaleMin = ((ValueConnectionKnob)dynamicConnectionPorts[0]).GetValue<float>();
            }
            if (((ValueConnectionKnob)dynamicConnectionPorts[1]).connected())
            {
                rescaleMax = ((ValueConnectionKnob)dynamicConnectionPorts[1]).GetValue<float>();
            }
        }
        float val = rawMIDIValue;
        if (rescale)
        {
            val = Mathf.Lerp(rescaleMin, rescaleMax, rawMIDIValue);
        }
        valueKnob.SetValue(val);
        return true;
    }
}
