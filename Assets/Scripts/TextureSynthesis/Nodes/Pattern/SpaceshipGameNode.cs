using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using SpaceshipGame;
using MiniGame;

// Class name and node ID intentionally kept as "SpaceshipGameNode" so canvases saved before
// the multi-mode refactor still deserialize their connections. The user-facing title reads
// "MiniGame" now that the node hosts multiple game modes behind a radio button.
[Node(false, "Pattern/SpaceshipGame")]
public class SpaceshipGameNode : TickingNode
{
    public const string ID = "SpaceshipGameNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "MiniGame"; } }

    // Positions 40/80 kept exactly as they were pre-refactor so saved-canvas connections
    // to these two ports still resolve; new outputs are appended at higher positions.
    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob gameOutputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 80)]
    public ValueConnectionKnob fluidVelocityOutputKnob;

    // BeaconGame-mode pulse outputs. 1.0 for one tick when the event fires, 0 otherwise.
    // Always 0 in SpaceshipGame mode.
    [ValueConnectionKnob("levelUp", Direction.Out, typeof(float), NodeSide.Bottom, 120)]
    public ValueConnectionKnob levelUpPulseKnob;

    [ValueConnectionKnob("collect", Direction.Out, typeof(float), NodeSide.Bottom, 160)]
    public ValueConnectionKnob beaconCollectedPulseKnob;

    private const float BaseWidth = 220f;
    private const float BaseHeight = 260f;
    private const float PlayerRowHeight = 22f;

    private Vector2 _DefaultSize = new Vector2(BaseWidth, BaseHeight);
    public override Vector2 DefaultSize => _DefaultSize;

    // Persisted with the canvas. On the first DoCalc tick after load we push this into
    // MiniGameController so the runtime mode matches what the canvas remembers.
    public RadioButtonSet gameModeSelection;

    // Canonical mapping between radio-button labels and the GameMode enum. Kept here so the
    // node UI and the coordinator can't drift.
    private static readonly Dictionary<string, GameMode> ModeByName = new Dictionary<string, GameMode>()
    {
        { "SpaceshipGame", GameMode.SpaceshipGame },
        { "BeaconGame", GameMode.BeaconGame },
    };

    // Last mode we pushed to MiniGameController; only invoke SetMode on a change so we don't
    // tear down the active game every tick.
    private string lastPushedMode;

    // Coordinator is optional: if it's not in the scene, the node degrades to plain
    // SpaceshipGame behavior (identical to pre-refactor). This lets canvases keep running
    // even when the beacon-game scene setup hasn't been added yet.
    private MiniGameController miniController;
    private SpaceshipGameController gameController;

    // Dynamic input ports added by the user, one per canvas player.
    private List<ValueConnectionKnob> PlayerPorts =>
        dynamicConnectionPorts.OfType<ValueConnectionKnob>()
            .Where(p => p.valueType == typeof(SpaceshipGamePlayerData)).ToList();

    public override void DoInit()
    {
        gameController = SpaceshipGameController.instance;
        miniController = MiniGameController.instance;
        if (gameModeSelection == null || gameModeSelection.names == null || gameModeSelection.names.Count == 0)
            gameModeSelection = new RadioButtonSet(0, "SpaceshipGame", "BeaconGame");
    }

    private void AddPlayerPort()
    {
        var attr = new ValueConnectionKnobAttribute(
            "Player", Direction.In, typeof(SpaceshipGamePlayerData), NodeSide.Left);
        CreateValueConnectionKnob(attr);
        SetSize();
    }

    private void SetSize()
    {
        float height = BaseHeight + PlayerRowHeight * PlayerPorts.Count;
        _DefaultSize = new Vector2(BaseWidth, height);
    }

    public override void NodeGUI()
    {
        SetSize();
        GUILayout.BeginVertical();

        GUILayout.Label("Game mode");
        RadioButtons(gameModeSelection);

        GUILayout.Space(6);

        // One row per canvas player: [knob] Pn ●/○ ... [×]
        var players = PlayerPorts;
        GUILayout.Label("Canvas players");
        ValueConnectionKnob toRemove = null;
        for (int i = 0; i < players.Count; i++)
        {
            var port = players[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("P{0} {1}", i, port.connected() ? "●" : "○"),
                GUILayout.Width(60));
            port.SetPosition();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(22)))
                toRemove = port;
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Add canvas player"))
            AddPlayerPort();
        if (toRemove != null)
        {
            DeleteConnectionPort(toRemove);
            SetSize();
        }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Texture previewTex = miniController != null
            ? miniController.GameBoardTex
            : (gameController != null ? gameController.gameBoardTex : null);
        if (previewTex != null)
            GUILayout.Box(previewTex, GUILayout.MaxHeight(128), GUILayout.MaxWidth(128));
        else
            GUILayout.Label("(no game controller in scene)");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (miniController == null)
            miniController = MiniGameController.instance;
        if (gameController == null)
            gameController = SpaceshipGameController.instance;

        // With a coordinator: full multi-mode behavior. Without: identical to the pre-refactor
        // node, so a scene that hasn't been upgraded yet still runs the spaceship game.
        if (miniController != null)
            return DoCalcCoordinated();
        return DoCalcDirect();
    }

    private bool DoCalcCoordinated()
    {
        string selected = gameModeSelection?.Selected;
        if (!string.IsNullOrEmpty(selected) && selected != lastPushedMode)
        {
            if (ModeByName.TryGetValue(selected, out var mode))
            {
                miniController.SetMode(mode);
                lastPushedMode = selected;
            }
        }

        gameOutputKnob.SetValue<Texture>(miniController.GameBoardTex);
        fluidVelocityOutputKnob.SetValue<Texture>(miniController.FluidVelocityTex);
        levelUpPulseKnob.SetValue<float>(miniController.LevelUpThisFrame ? 1f : 0f);
        beaconCollectedPulseKnob.SetValue<float>(miniController.BeaconCollectedThisFrame ? 1f : 0f);
        miniController.ClearFrameEvents();

        var activeIds = new HashSet<string>();
        foreach (var port in PlayerPorts)
        {
            if (!port.connected())
                continue;
            var data = port.GetValue<SpaceshipGamePlayerData>();
            if (string.IsNullOrEmpty(data.playerId))
                continue;
            miniController.ApplyCanvasInput(data);
            activeIds.Add(data.playerId);
        }
        miniController.ReconcileCanvasPlayers(activeIds);

        return true;
    }

    private bool DoCalcDirect()
    {
        if (gameController == null)
            return true;

        gameOutputKnob.SetValue<Texture>(gameController.gameBoardTex);
        fluidVelocityOutputKnob.SetValue<Texture>(gameController.fluidVelocityTex);
        // No coordinator, no beacon game — new outputs stay silent.
        levelUpPulseKnob.SetValue<float>(0f);
        beaconCollectedPulseKnob.SetValue<float>(0f);

        var activeIds = new HashSet<string>();
        foreach (var port in PlayerPorts)
        {
            if (!port.connected())
                continue;
            var data = port.GetValue<SpaceshipGamePlayerData>();
            if (string.IsNullOrEmpty(data.playerId))
                continue;
            gameController.ApplyCanvasInput(data);
            activeIds.Add(data.playerId);
        }
        gameController.ReconcileCanvasPlayers(activeIds);

        return true;
    }
}
