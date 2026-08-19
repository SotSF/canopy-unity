
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Audio/BandAvg")]
public class BandAvgNode : SignalNode
{
    public override string GetID => "BandAvgNode";
    public override string Title { get { return "BandAvg"; } }

    private Vector2 _DefaultSize = new Vector2(150, 100);

    protected override Vector2 BaseDefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrumData", Direction.In, typeof(float[]), NodeSide.Left)]
    public ValueConnectionKnob spectrumDataKnob;

    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;

    protected override IEnumerable<SignalChannel> GetSignalChannels()
    {
        yield return new SignalChannel
        {
            outputKnob = outputSignalKnob,
            getValue   = () => outputSignalKnob.GetValue<float>(),
            label      = "Output",
        };
    }

    public int filterLowEnd;
    public int filterHighEnd;

    private int spectrumSize;
    private float outputSignal;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        filterLowEnd = RTEditorGUI.IntSlider(filterLowEnd, 0, filterHighEnd);
        filterHighEnd = RTEditorGUI.IntSlider(filterHighEnd, filterLowEnd, spectrumSize);
        GUILayout.BeginHorizontal();
        spectrumDataKnob.DisplayLayout();
        GUILayout.EndHorizontal();
        DrawSparkline();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        var spectrum = spectrumDataKnob.GetValue<float[]>();
        if (spectrum != null)
        {
            float sum = 0;
            spectrumSize = spectrum.Length;
            // Bounds-clamped (upstream band count can shrink below a saved range) and
            // guarded against low == high, which used to emit 0/0 = NaN
            int lo = Mathf.Clamp(filterLowEnd, 0, spectrum.Length);
            int hi = Mathf.Clamp(filterHighEnd, lo, spectrum.Length);
            for (int i = lo; i < hi; i++)
            {
                sum += spectrum[i];
            }
            outputSignal = hi > lo ? sum / (hi - lo) : 0f;
        }
        outputSignalKnob.SetValue(outputSignal);
        return true;
    }
}
