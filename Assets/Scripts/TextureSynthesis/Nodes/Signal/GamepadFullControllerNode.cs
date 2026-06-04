using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

[Node(false, "Signal/GamepadFullController")]
public class GamepadFullControllerNode : TickingNode
{
    public override string GetID => "GamepadFullControllerNode";
    public override string Title { get { return "GamepadFullController"; } }

    private Vector2 _DefaultSize = new Vector2(250, 480);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("LeftStick", Direction.Out, typeof(Vector2), NodeSide.Right)]
    public ValueConnectionKnob LeftStickKnob;
    [ValueConnectionKnob("RightStick", Direction.Out, typeof(Vector2), NodeSide.Right)]
    public ValueConnectionKnob RightStickKnob;
    [ValueConnectionKnob("LeftTrigger", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob LeftTriggerKnob;
    [ValueConnectionKnob("RightTrigger", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob RightTriggerKnob;

    [ValueConnectionKnob("dpadUp", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadUpKnob;
    [ValueConnectionKnob("dpadDown", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadDownKnob;
    [ValueConnectionKnob("dpadLeft", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadLeftKnob;
    [ValueConnectionKnob("dpadRight", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadRightKnob;

    [ValueConnectionKnob("a", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob aKnob;
    [ValueConnectionKnob("b", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob bKnob;
    [ValueConnectionKnob("x", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob xKnob;
    [ValueConnectionKnob("y", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob yKnob;

    [ValueConnectionKnob("leftBumper", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob leftBumperKnob;
    [ValueConnectionKnob("rightBumper", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob rightBumperKnob;

    [ValueConnectionKnob("leftStickPress", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob leftStickPressKnob;
    [ValueConnectionKnob("rightStickPress", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob rightStickPressKnob;

    [ValueConnectionKnob("start", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob startKnob;
    [ValueConnectionKnob("back", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob backKnob;

    public string boundDeviceName = "";

    [NonSerialized] private RadioButtonSet controllerChoice;
    [NonSerialized] private string[] lastDeviceNames;

    // Live trigger traces, captured/rendered each tick (see DoCalc), drawn in NodeGUI.
    [NonSerialized] private SparklineTrace leftTriggerTrace;
    [NonSerialized] private SparklineTrace rightTriggerTrace;

    private void EnsureTraces()
    {
        // Fixed 0..1 range so a resting trigger reads flat along the bottom instead of
        // auto-scaling noise to fill the box.
        leftTriggerTrace  ??= new SparklineTrace(new Color(0.40f, 0.85f, 1.00f), fixedRange: new Vector2(0f, 1f));
        rightTriggerTrace ??= new SparklineTrace(new Color(1.00f, 0.55f, 0.30f), fixedRange: new Vector2(0f, 1f));
    }

    static GUIStyle _valueStyle, _boolOnStyle, _boolOffStyle;
    static GUIStyle ValueStyle   => _valueStyle  ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
    static GUIStyle BoolOnStyle  => _boolOnStyle  ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, normal = { textColor = new Color(0.45f, 0.95f, 0.45f) } };
    static GUIStyle BoolOffStyle => _boolOffStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, normal = { textColor = new Color(0.50f, 0.50f, 0.50f) } };

    // Fast Enter Play Mode keeps statics alive across sessions; drop cached styles so they
    // rebuild against the freshly-skinned GUI.skin on the next NodeGUI pass.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _valueStyle = _boolOnStyle = _boolOffStyle = null;
    }

    public override void NodeGUI()
    {
        EnsureControllerChoice();

        GUILayout.BeginVertical();

        bool boundIsConnected = !string.IsNullOrEmpty(boundDeviceName)
            && lastDeviceNames != null
            && Array.IndexOf(lastDeviceNames, boundDeviceName) >= 0;

        if (!string.IsNullOrEmpty(boundDeviceName) && !boundIsConnected)
            GUILayout.Label("Bound: " + boundDeviceName + " (disconnected)");

        if (controllerChoice.names.Count == 0)
        {
            GUILayout.Label("(no gamepads connected)");
        }
        else
        {
            RadioButtonsVertical(controllerChoice);
            var sel = controllerChoice.Selected;
            if (!string.IsNullOrEmpty(sel) && sel != boundDeviceName)
                boundDeviceName = sel;
        }

        EnsureTraces();

        StickRow(LeftStickKnob);
        StickRow(RightStickKnob);
        TriggerRow(LeftTriggerKnob, leftTriggerTrace);
        TriggerRow(RightTriggerKnob, rightTriggerTrace);
        BoolRow(dpadUpKnob);
        BoolRow(dpadDownKnob);
        BoolRow(dpadLeftKnob);
        BoolRow(dpadRightKnob);
        BoolRow(aKnob);
        BoolRow(bKnob);
        BoolRow(xKnob);
        BoolRow(yKnob);
        BoolRow(leftBumperKnob);
        BoolRow(rightBumperKnob);
        BoolRow(leftStickPressKnob);
        BoolRow(rightStickPressKnob);
        BoolRow(startKnob);
        BoolRow(backKnob);

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    // Each row draws the live value on the left, then the knob's name + output port at the
    // node's right edge (DisplayLayout positions the port from the label's row).
    private void StickRow(ValueConnectionKnob knob)
    {
        var v = knob.GetValue<Vector2>();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<{v.x,5:0.00}, {v.y,5:0.00}>", ValueStyle);
        GUILayout.FlexibleSpace();
        knob.DisplayLayout();
        GUILayout.EndHorizontal();
    }

    private void TriggerRow(ValueConnectionKnob knob, SparklineTrace trace)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(trace.TexHeight + 2));
        if (trace.Texture != null)
            GUILayout.Box(trace.Texture, GUILayout.ExpandWidth(true), GUILayout.MinWidth(40),
                GUILayout.Height(trace.TexHeight));
        else
            GUILayout.FlexibleSpace();
        GUILayout.Label(knob.GetValue<float>().ToString("0.00"), ValueStyle, GUILayout.Width(34));
        knob.DisplayLayout();
        GUILayout.EndHorizontal();
    }

    private void BoolRow(ValueConnectionKnob knob)
    {
        bool on = knob.GetValue<bool>();
        GUILayout.BeginHorizontal();
        GUILayout.Label(on ? "true" : "false", on ? BoolOnStyle : BoolOffStyle, GUILayout.Width(40));
        GUILayout.FlexibleSpace();
        knob.DisplayLayout();
        GUILayout.EndHorizontal();
    }

    private void EnsureControllerChoice()
    {
        var currentNames = BuildDeviceLabels();
        if (controllerChoice != null && lastDeviceNames != null && currentNames.SequenceEqual(lastDeviceNames))
            return;

        lastDeviceNames = currentNames;
        controllerChoice = new RadioButtonSet(currentNames);
        for (int i = 0; i < currentNames.Length; i++)
        {
            if (currentNames[i] == boundDeviceName)
            {
                controllerChoice.SelectOption(i);
                break;
            }
        }
    }

    private Gamepad GetBoundGamepad()
    {
        if (string.IsNullOrEmpty(boundDeviceName)) return null;
        var labels = BuildDeviceLabels();
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == boundDeviceName)
                return Gamepad.all[i];
        }
        return null;
    }

    // On Windows, XInput devices (Xbox controllers, and anything XInput-emulating like
    // post-shutdown Stadia) all report through the InputSystem's XInput backend, which
    // does NOT expose HID product/manufacturer strings -- description.product is empty
    // and displayName is the generic layout name "Xbox Controller". The only stable
    // disambiguator the API surfaces is the XInput user-index (slot 0..3) buried inside
    // description.capabilities JSON. We use that to label as "Xbox Controller (P1)" etc.
    // Then we still apply #N dedup as a final fallback for non-XInput pads.
    private static string[] BuildDeviceLabels()
    {
        var labels = new string[Gamepad.all.Count];
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            var gp = Gamepad.all[i];
            var product = gp.description.product;
            var baseName = string.IsNullOrEmpty(product) ? gp.displayName : product;
            int? slot = ExtractXInputUserIndex(gp.description.capabilities);
            labels[i] = slot.HasValue ? $"{baseName} (P{slot.Value + 1})" : baseName;
        }
        var seen = new Dictionary<string, int>();
        for (int i = 0; i < labels.Length; i++)
        {
            if (seen.TryGetValue(labels[i], out int n))
            {
                seen[labels[i]] = n + 1;
                labels[i] = labels[i] + " #" + (n + 1);
            }
            else
            {
                seen[labels[i]] = 1;
            }
        }
        return labels;
    }

    private static readonly Regex UserIndexRegex = new Regex("\"userIndex\"\\s*:\\s*(\\d+)");

    private static int? ExtractXInputUserIndex(string capabilitiesJson)
    {
        if (string.IsNullOrEmpty(capabilitiesJson)) return null;
        var m = UserIndexRegex.Match(capabilitiesJson);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int idx))
            return idx;
        return null;
    }

    public override bool DoCalc()
    {
        EnsureTraces();
        var gp = GetBoundGamepad();
        float lt = 0f, rt = 0f;

        if (gp != null)
        {
            LeftStickKnob.SetValue(gp.leftStick.ReadValue());
            RightStickKnob.SetValue(gp.rightStick.ReadValue());
            lt = gp.leftTrigger.ReadValue();
            rt = gp.rightTrigger.ReadValue();
            LeftTriggerKnob.SetValue(lt);
            RightTriggerKnob.SetValue(rt);

            dpadUpKnob.SetValue(gp.dpad.up.isPressed);
            dpadDownKnob.SetValue(gp.dpad.down.isPressed);
            dpadLeftKnob.SetValue(gp.dpad.left.isPressed);
            dpadRightKnob.SetValue(gp.dpad.right.isPressed);

            aKnob.SetValue(gp.aButton.isPressed);
            bKnob.SetValue(gp.bButton.isPressed);
            xKnob.SetValue(gp.xButton.isPressed);
            yKnob.SetValue(gp.yButton.isPressed);

            leftBumperKnob.SetValue(gp.leftShoulder.isPressed);
            rightBumperKnob.SetValue(gp.rightShoulder.isPressed);

            leftStickPressKnob.SetValue(gp.leftStickButton.isPressed);
            rightStickPressKnob.SetValue(gp.rightStickButton.isPressed);

            startKnob.SetValue(gp.startButton.isPressed);
            backKnob.SetValue(gp.selectButton.isPressed);
        }

        // Capture even when disconnected so the trace shows a flat zero rather than freezing.
        float zoom = (NodeEditor.curEditorState != null) ? NodeEditor.curEditorState.zoom : 1f;
        leftTriggerTrace.Capture(lt);
        rightTriggerTrace.Capture(rt);
        leftTriggerTrace.Render(zoom);
        rightTriggerTrace.Render(zoom);

        return true;
    }

    public void OnDestroy()
    {
        leftTriggerTrace?.Release();
        rightTriggerTrace?.Release();
        leftTriggerTrace = rightTriggerTrace = null;
    }
}
