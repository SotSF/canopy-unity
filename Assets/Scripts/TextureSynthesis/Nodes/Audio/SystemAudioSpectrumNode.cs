using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;
using Lasp;

[Node(false, "Audio/SystemAudioSpectrum")]
public class SystemAudioSpectrumNode : TickingNode
{
    public override string GetID => "SystemAudioSpectrumNode";
    public override string Title { get { return "SystemAudioSpectrum"; } }

    private Vector2 _DefaultSize = new Vector2(220, 190);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    [ValueConnectionKnob("sampleRate", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob sampleRateKnob;

    [ValueConnectionKnob("attackTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob attackTauKnob;

    [ValueConnectionKnob("releaseTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob releaseTauKnob;

    public float attackTau  = 0.04f;
    public float releaseTau = 0.25f;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        var capture = SystemAudioCapture.Instance;
        bool running = capture != null && capture.IsRunning;
        GUILayout.Label(capture == null ? "No capture instance" : running ? "Capturing" : "Not capturing");

        GUI.enabled = capture != null;
        if (GUILayout.Button(running ? "Stop capture" : "Start capture"))
        {
            if (running) capture.StopCapture();
            else capture.StartCapture();
        }
        GUI.enabled = true;

        spectrumDataKnob.DisplayLayout();
        sampleRateKnob.DisplayLayout();

        FloatKnobOrSlider(ref attackTau,  0f, 1f, attackTauKnob);
        FloatKnobOrSlider(ref releaseTau, 0f, 2f, releaseTauKnob);

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        var capture = SystemAudioCapture.Instance;
        if (capture == null || !capture.IsRunning) return false;
        spectrumDataKnob.SetValue(capture.Spectrum);
        sampleRateKnob.SetValue((float)capture.SampleRate);
        capture.AttackTau  = attackTauKnob.connected()  ? attackTauKnob.GetValue<float>()  : attackTau;
        capture.ReleaseTau = releaseTauKnob.connected() ? releaseTauKnob.GetValue<float>() : releaseTau;
        return true;
    }
}
