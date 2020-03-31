
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using UnityEngine;

[Node(false, "Signal/SignalLooper")]
public class SignalLooperNode : Node
{
    public override string GetID => "SignalLooperNode";
    public override string Title { get { return "SignalLooper"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 150); } }

    [ValueConnectionKnob("inputSignal", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob inputSignalKnob;
    [ValueConnectionKnob("frequencyModulation", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob frequencyModulationKnob;
    [ValueConnectionKnob("control", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob recordControlKnob;
    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;


    public bool clipRecorded;
    public bool clipRecording;
    private bool clipPlaying;

    public float frequencyModulation = .5f;

    public float clipLength;

    private float recordingStarted;
    private float playbackTime = 0;

    public List<float> clipSamples;

    public void Awake()
    {
        if (clipSamples == null)
        {
            clipSamples = new List<float>();
        }
    }

    public void StartRecording()
    {
        recordingStarted = Time.time;
        clipRecording = true;
    }

    public void FinishRecording()
    {
        clipRecorded = true;
        clipRecording = false;
        clipLength = Time.time - recordingStarted;
    }

    private void ResetRecording()
    {
        clipRecorded = false;
        clipRecording = false;
        clipPlaying = false;
        clipSamples.Clear();
        playbackTime = 0;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        inputSignalKnob.DisplayLayout();
        GUILayout.BeginHorizontal();
        recordControlKnob.DisplayLayout();
        // Start state: neither recorded nor recording
        if(!clipRecorded && !clipRecording)
        {
            // Display start recording button
            if (GUILayout.Button("Start recording"))
            {
                StartRecording();
            }
        } else
        {
            // Second state: recording input
            if (clipRecording)
            {
                // Display end recording button
                if (GUILayout.Button("Finish"))
                {
                    FinishRecording();
                }
            } 
            // Third state: recording is finished
            else
            {
                // Display Start/stop button
                string label = clipPlaying ? "Stop" : "Start";
                if (GUILayout.Button(label))
                {
                    TogglePlayback();
                }
            }
        }
        GUILayout.EndHorizontal();

        frequencyModulationKnob.DisplayLayout();
        if (!frequencyModulationKnob.connected())
        {
            frequencyModulation = RTEditorGUI.Slider(frequencyModulation, 0, 1);
        }

        if (clipRecorded)
        {
            if (GUILayout.Button("Reset"))
            {
                ResetRecording();
            }
        }

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void TogglePlayback()
    {
        playbackTime = 0;
        clipPlaying = !clipPlaying;
    }

    private void HandleInput()
    {
        if (!clipRecorded && !clipRecording)
        {
            StartRecording();
        } else if (!clipRecorded && clipRecording)
        {
            FinishRecording();
        } else if (clipRecorded)
        {
            TogglePlayback();
        }
    }

    public override bool Calculate()
    {
        // Handle event input controls
        if (recordControlKnob.GetValue<bool>())
        {
            HandleInput();
        }

        if (clipRecorded)
        {
            if (clipPlaying)
            {
                frequencyModulation = frequencyModulationKnob.connected() ? frequencyModulationKnob.GetValue<float>() : frequencyModulation;
                float freqMultiplier = 1;
                if (frequencyModulation < 0.5f)
                {
                    freqMultiplier = Mathf.Lerp(0.125f, 1, frequencyModulation * 2);
                } else
                {
                    freqMultiplier = Mathf.Lerp(1, 8, (frequencyModulation-.5f) * 2);
                }

                playbackTime += Time.deltaTime * freqMultiplier;
                var normalizedPlaybackPosition = Mathf.InverseLerp(0, clipLength, playbackTime % clipLength);
                var floatingIndex = normalizedPlaybackPosition * (clipSamples.Count-1);
                var i0 = Mathf.FloorToInt(floatingIndex);
                var i1 = (i0 + 1) % clipSamples.Count;
                var value = Mathf.Lerp(clipSamples[i0], clipSamples[i1], floatingIndex % 1);
                outputSignalKnob.SetValue(value);
                return true;
            }
        }
        else if (clipRecording)
        {
            clipSamples.Add(inputSignalKnob.GetValue<float>());
        }
        return true;
    }
}
