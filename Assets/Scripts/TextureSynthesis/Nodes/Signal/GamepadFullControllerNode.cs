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

    private Vector2 _DefaultSize = new Vector2(220, 440);
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

    [ValueConnectionKnob("start", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob startKnob;
    [ValueConnectionKnob("back", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob backKnob;

    public string boundDeviceName = "";

    [NonSerialized] private RadioButtonSet controllerChoice;
    [NonSerialized] private string[] lastDeviceNames;

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

        LeftStickKnob.DisplayLayout();
        RightStickKnob.DisplayLayout();
        LeftTriggerKnob.DisplayLayout();
        RightTriggerKnob.DisplayLayout();
        dpadUpKnob.DisplayLayout();
        dpadDownKnob.DisplayLayout();
        dpadLeftKnob.DisplayLayout();
        dpadRightKnob.DisplayLayout();
        aKnob.DisplayLayout();
        bKnob.DisplayLayout();
        xKnob.DisplayLayout();
        yKnob.DisplayLayout();
        leftBumperKnob.DisplayLayout();
        rightBumperKnob.DisplayLayout();
        startKnob.DisplayLayout();
        backKnob.DisplayLayout();

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
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
        var gp = GetBoundGamepad();
        if (gp == null) return true;

        LeftStickKnob.SetValue(gp.leftStick.ReadValue());
        RightStickKnob.SetValue(gp.rightStick.ReadValue());
        LeftTriggerKnob.SetValue(gp.leftTrigger.ReadValue());
        RightTriggerKnob.SetValue(gp.rightTrigger.ReadValue());

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

        startKnob.SetValue(gp.startButton.isPressed);
        backKnob.SetValue(gp.selectButton.isPressed);

        return true;
    }
}
