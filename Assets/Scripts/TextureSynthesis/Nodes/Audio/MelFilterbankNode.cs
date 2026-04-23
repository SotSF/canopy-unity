using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using Lasp;

[Node(false, "Audio/MelFilterbank")]
public class MelFilterbankNode : TickingNode
{
    public override string GetID => "MelFilterbankNode";
    public override string Title { get { return "MelFilterbank"; } }

    private Vector2 _DefaultSize = new Vector2(220, 170);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("spectrum", Direction.In, typeof(float[]), NodeSide.Left)]
    public ValueConnectionKnob spectrumKnob;

    [ValueConnectionKnob("sampleRate", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob sampleRateKnob;

    [ValueConnectionKnob("melSpectrum", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob melSpectrumKnob;

    public int bandCount = 64;
    public int minHz = 40;
    public int maxHz = 8000;
    public int sampleRate = 48000;

    [System.NonSerialized] private MelFilterbank _filterbank;
    [System.NonSerialized] private int _filterbankFftBins;
    [System.NonSerialized] private int _filterbankSampleRate;
    [System.NonSerialized] private int _filterbankBandCount;
    [System.NonSerialized] private float _filterbankMinHz;
    [System.NonSerialized] private float _filterbankMaxHz;
    [System.NonSerialized] private float[] _output;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        spectrumKnob.DisplayLayout();
        melSpectrumKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        sampleRateKnob.DisplayLayout();
        if (!sampleRateKnob.connected())
        {
            sampleRate = RTEditorGUI.IntField(sampleRate);
        }
        else
        {
            GUILayout.Label($"{sampleRate} Hz");
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Bands");
        bandCount = RTEditorGUI.IntSlider(bandCount, 4, 256);

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("Min Hz");
        minHz = RTEditorGUI.IntField(minHz);
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        GUILayout.Label("Max Hz");
        maxHz = RTEditorGUI.IntField(maxHz);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        var spectrum = spectrumKnob.GetValue<float[]>();
        if (spectrum == null || spectrum.Length == 0) return false;

        if (sampleRateKnob.connected())
        {
            int sr = Mathf.RoundToInt(sampleRateKnob.GetValue<float>());
            if (sr > 0) sampleRate = sr;
        }

        if (bandCount < 1) bandCount = 1;
        if (maxHz <= minHz) maxHz = minHz + 1;

        bool needsRebuild = _filterbank == null
            || _filterbankFftBins != spectrum.Length
            || _filterbankSampleRate != sampleRate
            || _filterbankBandCount != bandCount
            || _filterbankMinHz != minHz
            || _filterbankMaxHz != maxHz;

        if (needsRebuild)
        {
            _filterbank = new MelFilterbank(spectrum.Length, sampleRate, bandCount, minHz, maxHz);
            _filterbankFftBins = spectrum.Length;
            _filterbankSampleRate = sampleRate;
            _filterbankBandCount = bandCount;
            _filterbankMinHz = minHz;
            _filterbankMaxHz = maxHz;
            _output = new float[bandCount];
        }

        _filterbank.Apply(spectrum, _output);
        melSpectrumKnob.SetValue(_output);
        return true;
    }
}
