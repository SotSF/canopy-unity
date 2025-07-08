using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using TexSynth.Audio.Core;
using System.Collections.Generic;

[Node(false, "Audio/LaspAudioSpectrum")]
public class LaspAudioSpectrumNode : TickingNode
{
    public override string GetID => "LaspAudioSpectrumNode";
    public override string Title { get { return "LaspAudioSpectrum"; } }

    private Vector2 _DefaultSize = new Vector2(200, 160);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    public bool capturing = false;
    public RadioButtonSet scalingModeSelection;
    public RadioButtonSet captureModeSelection;

    public override void DoInit()
    {
        if (scalingModeSelection == null || scalingModeSelection.names.Count == 0)
        {
            scalingModeSelection = new RadioButtonSet(1, "Log", "Linear");
        }
    }


    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        // 1st row
        GUILayout.BeginHorizontal();

        // Spectrum scaling mode - sqrt/decibel/linear
        GUILayout.BeginVertical();
        GUILayout.Label("Scaling");
        RadioButtons(scalingModeSelection);
        GUILayout.EndVertical();


        GUILayout.EndHorizontal();


        GUILayout.BeginHorizontal();

        spectrumDataKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }




    public override bool DoCalc()
    {
        if (scalingModeSelection.Selected == "Log")
        {
            spectrumDataKnob.SetValue(LASPAudioManager.spectrumAnalyzer.logSpectrumSpan.ToArray());
        }
        else
        {
            spectrumDataKnob.SetValue(LASPAudioManager.spectrumAnalyzer.spectrumSpan.ToArray());
        }
        return true;
    }
}
