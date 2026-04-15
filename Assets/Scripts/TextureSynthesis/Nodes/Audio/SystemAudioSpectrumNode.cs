using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;
using Lasp;

[Node(false, "Audio/SystemAudioSpectrum")]
public class SystemAudioSpectrumNode : TickingNode
{
    public override string GetID => "SystemAudioSpectrumNode";
    public override string Title { get { return "SystemAudioSpectrum"; } }

    private Vector2 _DefaultSize = new Vector2(200, 100);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        var capture = SystemAudioCapture.Instance;
        GUILayout.Label(capture != null && capture.IsRunning ? "Capturing" : "Not capturing");
        spectrumDataKnob.DisplayLayout();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        var capture = SystemAudioCapture.Instance;
        if (capture == null || !capture.IsRunning) return false;
        spectrumDataKnob.SetValue(capture.Spectrum);
        return true;
    }
}
