﻿using NodeEditorFramework;
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

[Node(false, "Pattern/ConjurerInput")]
public class ConjurerInputNode : TickingNode
{
    public const string ID = "ConjurerInputNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "ConjurerInput"; } }


    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob texOutputKnob;
    private Vector2 _DefaultSize = new Vector2(180, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    private ConjurerController conjurerController;

    public override void DoInit()
    {
        conjurerController = ConjurerController.instance;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Box(conjurerController.inputTex, GUILayout.MaxHeight(96), GUILayout.MaxWidth(150));

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }



    public override bool DoCalc()
    {
        texOutputKnob.SetValue<Texture>(conjurerController.inputTex);
        return true;
    }
}