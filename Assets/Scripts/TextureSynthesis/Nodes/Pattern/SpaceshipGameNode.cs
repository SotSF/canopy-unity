using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;

using UnityEngine;
using UnityEngine.Networking;
using System.Threading;

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
    private Vector2 _DefaultSize = new Vector2(180, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    public bool check;

    private SpaceshipGameController gameController;

    public void Awake()
    {
        gameController = SpaceshipGameController.instance;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Box(gameController.gameBoardTex, GUILayout.MaxHeight(128), GUILayout.MaxWidth(128));

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }



    public override bool Calculate()
    {
        gameOutputKnob.SetValue<Texture>(gameController.gameBoardTex);
        fluidVelocityOutputKnob.SetValue<Texture>(gameController.fluidVelocityTex);
        return true;
    }
}