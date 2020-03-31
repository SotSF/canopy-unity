using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.DSP;
using CSCore.SoundIn;
using CSCore.Streams;
using TexSynth.Audio.Core;
using TexSynth.Audio.WasapiAudio;
using System.Collections.Generic;

[Node(false, "Audio/AudioSpectrum")]
public class AudioSpectrumNode : TickingNode
{
    public override string GetID => "SystemAudioSpectrumNode";
    public override string Title { get { return "SystemAudio"; } }

    public override Vector2 DefaultSize { get { return new Vector2(200, 175); } }

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    public bool capturing = false;
    public RadioButtonSet scalingModeSelection;
    public RadioButtonSet captureModeSelection;

    private WasapiAudio wasapiAudio;
    private SpectrumSmoother smoother;

    public WasapiCaptureType captureMode = WasapiCaptureType.Loopback;
    public ScalingStrategy scalingMode = ScalingStrategy.Decibel;
    public int spectrumSize = 32;
    public int minFreq = 32;
    public int maxFreq = 16384;
    public int smoothingIterations = 5;

    private int newMinFreq = 32;
    private int newMaxFreq = 16384;
    private int newSmoothing = 5;

    private Dictionary<string, WasapiCaptureType> labelToCapture = new Dictionary<string, WasapiCaptureType>()
    {
        {"System", WasapiCaptureType.Loopback },
        {"Mic", WasapiCaptureType.Microphone }
    };

    private Dictionary<string, ScalingStrategy> labelToScaling = new Dictionary<string, ScalingStrategy>()
    {
        {"Linear", ScalingStrategy.Linear },
        {"Decibel", ScalingStrategy.Decibel },
        {"Sqrt", ScalingStrategy.Sqrt }
    };

    private float[] spectrumData;

    public void Awake()
    {
        if (scalingModeSelection == null || scalingModeSelection.names.Count == 0)
        {
            scalingModeSelection = new RadioButtonSet(1, "Sqrt", "Decibel", "Linear");
        }
        if (captureModeSelection == null || captureModeSelection.names.Count == 0)
        {
            captureModeSelection = new RadioButtonSet(0, "System", "Mic");
        }
        InitializeWasapiCapture();
        InitializeSmoother();
        if (capturing)
        {
            wasapiAudio.StartListen();
        }
    }

    private void ReceiveSpectrum(float[] data)
    {
        spectrumData = data;
    }

    private void InitializeWasapiCapture()
    {
        wasapiAudio = new WasapiAudio(captureMode, spectrumSize, scalingMode, minFreq, maxFreq, ReceiveSpectrum);
    }

    private void InitializeSmoother()
    {
        smoother = new SpectrumSmoother(spectrumSize, smoothingIterations);
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        // Capture mode - system audio vs mic
        GUILayout.BeginHorizontal();
        RadioButtons(captureModeSelection);
        GUILayout.EndHorizontal();
        // Spectrum scaling mode - sqrt/decibel/linear
        GUILayout.BeginHorizontal();
        RadioButtons(scalingModeSelection);
        GUILayout.EndHorizontal();

        // Min/max frequency, smoothing iterations
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.Label("Min freq");
        newMinFreq = RTEditorGUI.IntField(minFreq);
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        GUILayout.Label("Max freq");
        newMaxFreq = RTEditorGUI.IntField(maxFreq);
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        GUILayout.Label("Smoothing");
        newSmoothing = RTEditorGUI.IntSlider(smoothingIterations, 1, 10);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        var label = capturing ? "Stop capture" : "Start capture";
        if (GUILayout.Button(label))
        {
            capturing = !capturing;
            if (!capturing)
            {
                wasapiAudio.StopListen();
            } else
            {
                InitializeWasapiCapture();
                wasapiAudio.StartListen();
            }
        }

        spectrumDataKnob.DisplayLayout();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void CheckCaptureParams()
    {
        bool freqChanged = newMinFreq != minFreq || newMaxFreq != maxFreq;
        bool scalingChanged = labelToScaling[scalingModeSelection.Selected] != scalingMode;
        bool deviceChanged = labelToCapture[captureModeSelection.Selected] != captureMode;
        bool smoothingChanged = newSmoothing != smoothingIterations;
        if (freqChanged)
        {
            minFreq = newMinFreq;
            maxFreq = newMaxFreq;
        }
        if (scalingChanged)
        {
            scalingMode = labelToScaling[scalingModeSelection.Selected];
        }
        if (deviceChanged)
        {
            captureMode = labelToCapture[captureModeSelection.Selected];
        }
        if (freqChanged || scalingChanged || deviceChanged)
        {
            if (wasapiAudio != null && capturing)
            {
                wasapiAudio.StopListen();
            }
            InitializeWasapiCapture();
            if (capturing)
            {
                wasapiAudio.StartListen();
            }
        }
        if (smoothingChanged)
        {
            smoothingIterations = newSmoothing;
            InitializeSmoother();
        }
    }

    private float[] GetSpectrumData()
    {
        if (spectrumData != null)
        {
            return smoother.GetSpectrumData(spectrumData);
        }
        return null;
    }
    
    public override bool Calculate()
    {
        CheckCaptureParams();
        smoother.AdvanceFrame();
        spectrumDataKnob.SetValue(GetSpectrumData());
        return true;
    }
}
