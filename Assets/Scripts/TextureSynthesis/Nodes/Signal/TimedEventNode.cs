
using System;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Signal/TimedEventNode")]
public class TimedEventNode : TickingNode
{
    public override string GetID => "TimedEventNode";
    public override string Title { get { return "TimedEvent"; } }

    private Vector2 _DefaultSize =new Vector2(220, 140);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("outputSignal", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob outputSignalKnob;
    public DateTime startTime;
    public TimeSpan timerDuration;
    public TimeSpan timeRemaining;
    public string timeSpanString;
    public bool hasTriggered = false;
    private bool outputEvent;
    public bool triedParse = false;
    public bool timeParseSuccess = false;
    public bool timerRunning = false;
    public bool timerPaused = false;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.Label(string.Format("Time to event (eg \"8:00:00 for 8 hrs\")", false));
        timeSpanString = GUILayout.TextField(timeSpanString);
        if (triedParse)
        {
            if (timeParseSuccess)
            {
                if (!hasTriggered)
                {
                    GUILayout.Label($"Time remaining: {timeRemaining}");
                }
                if (timerRunning)
                {
                    if (GUILayout.Button("Stop timer"))
                    {
                        timerRunning = false;
                        timerPaused = true;
                    }                    
                }
                else
                {
                    if (timerPaused)
                    {
                        if (GUILayout.Button("Restart timer"))
                        {
                            lastCalcTime = Time.time - Time.deltaTime;
                            timerRunning = true;
                            timerPaused = false;
                        }
                    }
                    else
                    {
                        GUILayout.Label("Timer complete");
                    }
                }
            }
            else 
            {
                GUILayout.Label("Failed to parse time string");
            }
        }
        if (GUILayout.Button("Begin timer"))
        {
            triedParse = true;
            timeParseSuccess = TimeSpan.TryParse(timeSpanString, out timerDuration);
            if (timeParseSuccess)
            {
                startTime = DateTime.Now;
                timeRemaining = timerDuration;
                timerRunning = true;
            }
        };
        outputSignalKnob.DisplayLayout();
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    float lastCalcTime = 0;
    public override bool Calculate()
    {
        float elapsedTime = 0;
        if (lastCalcTime > 0)
        {
            elapsedTime = Time.time - lastCalcTime;
        }
        lastCalcTime = Time.time;
        
        if (timeParseSuccess && timerRunning && hasTriggered == false && timeRemaining.TotalMilliseconds <= 0)
        {
            outputSignalKnob.SetValue(true);
            hasTriggered = true;
            timerRunning = false;
        }
        else
        {
            if (timerRunning)
            {
                timeRemaining -= TimeSpan.FromSeconds(elapsedTime);
            }
            outputSignalKnob.SetValue(false);
        }
        return true;
    }
}
