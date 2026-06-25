using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using SpaceshipGame;

[Node(false, "Pattern/SpaceshipGame")]
public class SpaceshipGameNode : TickingNode
{
    public const string ID = "SpaceshipGameNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "SpaceshipGame"; } }

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob gameOutputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 80)]
    public ValueConnectionKnob fluidVelocityOutputKnob;

    private const float BaseWidth = 220f;
    private const float BaseHeight = 200f;
    private const float PlayerRowHeight = 22f;

    private Vector2 _DefaultSize = new Vector2(BaseWidth, BaseHeight);
    public override Vector2 DefaultSize => _DefaultSize;

    private SpaceshipGameController gameController;

    // Dynamic input ports added by the user, one per canvas player.
    private List<ValueConnectionKnob> PlayerPorts =>
        dynamicConnectionPorts.OfType<ValueConnectionKnob>()
            .Where(p => p.valueType == typeof(SpaceshipGamePlayerData)).ToList();

    public override void DoInit()
    {
        gameController = SpaceshipGameController.instance;
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
        if (gameController != null)
            GUILayout.Box(gameController.gameBoardTex, GUILayout.MaxHeight(128), GUILayout.MaxWidth(128));
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
        if (gameController == null)
            gameController = SpaceshipGameController.instance;
        if (gameController == null)
            return true;

        gameOutputKnob.SetValue<Texture>(gameController.gameBoardTex);
        fluidVelocityOutputKnob.SetValue<Texture>(gameController.fluidVelocityTex);

        // Forward each connected canvas player's bundled input, and tell the controller
        // the full set of active players so it can retire any that are gone.
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
