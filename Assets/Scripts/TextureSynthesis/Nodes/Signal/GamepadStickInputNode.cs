using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

[Node(false, "Signal/GamepadStickInput")]
public class GamepadStickInputNode : SignalNode
{
    public enum GamepadStickId { Left, Right }

    public override string GetID => "GamepadStickInputNode";
    public override string Title { get { return "GamepadStickInput"; } }

    private Vector2 _DefaultSize = new Vector2(220, 140);
    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("axis2D", Direction.Out, typeof(Vector2), NodeSide.Right)]
    public ValueConnectionKnob axis2DKnob;
    [ValueConnectionKnob("axisX", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob axisXKnob;
    [ValueConnectionKnob("axisY", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob axisYKnob;

    bool binding = false;
    public bool bound = false;
    public GamepadStickId boundStick;
    public string boundDeviceName = "";

    private Vector2 axis2D;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = axisXKnob,
            getValue   = () => axis2D.x,
            label      = "X",
        };
        yield return new SignalChannel
        {
            outputKnob = axisYKnob,
            getValue   = () => axis2D.y,
            label      = "Y",
        };
        yield return new SignalChannel
        {
            outputKnob = axis2DKnob,
            getValue   = null, // port-only row; Vector2 doesn't sparkline meaningfully
            label      = "2D",
        };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind stick"))
                binding = true;
        }
        else if (binding)
        {
            GUILayout.Label("Move a thumbstick to bind");
        }
        else
        {
            if (GUILayout.Button("Unbind"))
                bound = false;

            GUILayout.Label(string.Format("{0} ({1})", boundDeviceName, boundStick));
            GUILayout.Label(string.Format("<{0:0.00}, {1:0.00}>", axis2D.x, axis2D.y));
        }

        DrawSparkline();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        const float epsilon = 0.000001f;

        if (binding)
        {
            var labels = BuildDeviceLabels();
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                var gp = Gamepad.all[i];
                Vector2 ls = gp.leftStick.ReadValue();
                if (ls.magnitude > epsilon)
                {
                    boundDeviceName = labels[i];
                    boundStick = GamepadStickId.Left;
                    binding = false;
                    bound = true;
                    break;
                }
                Vector2 rs = gp.rightStick.ReadValue();
                if (rs.magnitude > epsilon)
                {
                    boundDeviceName = labels[i];
                    boundStick = GamepadStickId.Right;
                    binding = false;
                    bound = true;
                    break;
                }
            }
        }
        else if (bound)
        {
            var labels = BuildDeviceLabels();
            Gamepad gp = null;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == boundDeviceName)
                {
                    gp = Gamepad.all[i];
                    break;
                }
            }
            if (gp == null) return true;

            axis2D = boundStick == GamepadStickId.Left
                ? gp.leftStick.ReadValue()
                : gp.rightStick.ReadValue();

            axis2DKnob.SetValue(axis2D);
            axisXKnob.SetValue(axis2D.x);
            axisYKnob.SetValue(axis2D.y);
        }
        return true;
    }

    // See GamepadFullControllerNode.BuildDeviceLabels for why we use XInput userIndex
    // for disambiguation -- description.product is empty for XInput devices on Windows.
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
}
