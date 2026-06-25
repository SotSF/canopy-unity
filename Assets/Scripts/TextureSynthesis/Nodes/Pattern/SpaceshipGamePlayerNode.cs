using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System;
using System.Collections.Generic;
using UnityEngine;
using SpaceshipGame;

// Bundles loose canvas signals (gamepad/MIDI/oscillator floats + button events) into a
// single SpaceshipGamePlayerData, so they can drive a "canvas player" ship in the game.
// Wire the output into a SpaceshipGame node's canvas-player port.
[Node(false, "Pattern/SpaceshipGamePlayer")]
public class SpaceshipGamePlayerNode : TickingNode
{
    public const string ID = "SpaceshipGamePlayerNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "SpaceshipGamePlayer"; } }

    private Vector2 _DefaultSize = new Vector2(230, 350);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("lx", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob lxKnob;
    [ValueConnectionKnob("ly", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob lyKnob;
    [ValueConnectionKnob("rx", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob rxKnob;
    [ValueConnectionKnob("ry", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob ryKnob;

    [ValueConnectionKnob("fire", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob fireKnob;
    [ValueConnectionKnob("altFire", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob altFireKnob;

    // Optional color source (e.g. a ColorPicker node). Vector4 RGBA, matching ChromaKey's
    // keyColor type. When unconnected the ship keeps its default color.
    [ValueConnectionKnob("color", Direction.In, typeof(Vector4), NodeSide.Left)]
    public ValueConnectionKnob colorKnob;

    [ValueConnectionKnob("playerData", Direction.Out, typeof(SpaceshipGamePlayerData), NodeSide.Right)]
    public ValueConnectionKnob playerDataKnob;

    // Manual fallbacks used when the matching input knob is unconnected.
    public float lx, ly, rx, ry;

    // Stable per-player identity, persisted with the canvas so the game tracks one ship per node.
    public string playerId;
    public RadioButtonSet playerType;

    private Dictionary<string, PlayerType> playerTypeMap = new Dictionary<string, PlayerType>()
    {
        {"Generic", PlayerType.GenericCanvas},
        {"Controller", PlayerType.Controller},
        {"Oddball", PlayerType.Oddball},
    };

    public override void DoInit()
    {
        if (string.IsNullOrEmpty(playerId))
            playerId = Guid.NewGuid().ToString();
        if (playerType == null || playerType.names.Count == 0)
        {
            playerType = new RadioButtonSet(1, "Generic", "Controller", "Oddball");
        }
    }

    public override void NodeGUI()
    {
        if (string.IsNullOrEmpty(playerId))
            playerId = Guid.NewGuid().ToString();

        GUILayout.BeginVertical();

        GUILayout.Label("Left stick");
        FloatKnobOrSlider(ref lx, -1, 1, lxKnob);
        FloatKnobOrSlider(ref ly, -1, 1, lyKnob);

        GUILayout.Label("Right stick");
        FloatKnobOrSlider(ref rx, -1, 1, rxKnob);
        FloatKnobOrSlider(ref ry, -1, 1, ryKnob);

        bool fire = EventKnobOrButton("Fire", fireKnob);
        bool altFire = EventKnobOrButton("Alt fire", altFireKnob);

        GUILayout.BeginHorizontal();
        colorKnob.DisplayLayout();
        if (colorKnob.connected())
        {
            Color c = colorKnob.GetValue<Vector4>();
            var prevColor = GUI.color;
            GUI.color = c;
            GUILayout.Box(Texture2D.whiteTexture, GUILayout.Width(28), GUILayout.Height(16));
            GUI.color = prevColor;
        }
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        playerDataKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        RadioButtons(playerType);
        GUILayout.EndVertical();

        // Buttons are level signals here; rising-edge detection happens in the controller.
        PushPlayerData(fire, altFire);

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        bool fire = fireKnob.connected() && fireKnob.GetValue<bool>();
        bool altFire = altFireKnob.connected() && altFireKnob.GetValue<bool>();
        PushPlayerData(fire, altFire);
        //var player = SpaceshipGameController.instance.GetPlayerById(playerId);
        //if (player != null && playerTypeMap[playerType.Selected] != player?.playerType)
        //{
        //    player.playerType = playerTypeMap[playerType.Selected];
        //}
        return true;
    }

    private void PushPlayerData(bool fire, bool altFire)
    {
        bool hasColor = colorKnob.connected();
        var data = new SpaceshipGamePlayerData
        {
            playerId = playerId,
            leftStick = new Vector2(
                lxKnob.connected() ? lxKnob.GetValue<float>() : lx,
                lyKnob.connected() ? lyKnob.GetValue<float>() : ly),
            rightStick = new Vector2(
                rxKnob.connected() ? rxKnob.GetValue<float>() : rx,
                ryKnob.connected() ? ryKnob.GetValue<float>() : ry),
            fire = fire,
            altFire = altFire,
            hasColor = hasColor,
            color = hasColor ? (Color)colorKnob.GetValue<Vector4>() : Color.white,
            playerType = playerTypeMap[playerType.Selected],
        };
        playerDataKnob.SetValue(data);
    }
}
