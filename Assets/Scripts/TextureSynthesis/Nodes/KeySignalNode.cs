using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

[Node(false, "KeyboardControls/KeySignal")]
public class KeySignalNode : TickingNode
{
    public override string GetID => "KeySignal";
    public override string Title { get { return "KeySignal"; } }
    public override Vector2 DefaultSize => new Vector2(150, 100);

    [ValueConnectionKnob("Out", Direction.Out, typeof(float))]
    public ValueConnectionKnob signalOutputKnob;

    HashSet<KeyCode> bindingKeys;
    HashSet<KeyCode> boundKeys;
    bool inputActive = false;

    bool useEasing = false;
    bool binding = false;
    bool bound = false;

    [NonSerialized]
    float timeDown = 0;
    [NonSerialized]
    float timeUp = 0;

    private void Awake()
    {
        bindingKeys = new HashSet<KeyCode>();
        boundKeys = new HashSet<KeyCode>();
    }

    public override bool Calculate()
    {
        if (useEasing)
        {
            float elapsed;
            var easingCurve = AnimationCurveSet.instance.curves[0];
            if (inputActive)
            {
                elapsed = Time.time - timeDown;
            }
            else
            {
                elapsed = Mathf.Min(1, timeUp - timeDown) - (Time.time - timeUp);
            }
            var easedOutput = easingCurve.Evaluate(elapsed);
            signalOutputKnob.SetValue(easedOutput);
        }
        else
        {
            signalOutputKnob.SetValue<float>(inputActive ? 1 : 0);
        }
        return true;
    }
         
    /* Use the IMGUI Event system to bind input keys.
       Gotchas: 
         - key-repeat means events get sent multiple times, thus the !inputActive check
         - Set(0) != Set(0), use SetEquals
         - Only trigger timeUp if the removed key was actually part of the bound key chord
         */
    void HandleInput()
    {
        var e = Event.current;
        if (e.keyCode == KeyCode.None)
            return;
        switch (e.type)
        {
            case EventType.KeyDown:
                if (binding)
                {
                    bindingKeys.Add(e.keyCode);
                } else if (bound && boundKeys.Contains(e.keyCode) && !inputActive)
                {
                    bindingKeys.Add(e.keyCode);
                    if (bindingKeys.SetEquals(boundKeys))
                    {
                        inputActive = true;
                        timeDown = Time.time;
                    }
                }
                break;
            case EventType.KeyUp:
                if (binding)
                {
                    bindingKeys.Remove(e.keyCode);
                    boundKeys.Add(e.keyCode);
                    if (bindingKeys.Count == 0)
                    {
                        binding = false;
                        bound = true;
                    }
                } else if (bound)
                {
                    var removedKey = bindingKeys.Remove(e.keyCode);
                    if (removedKey && inputActive)
                    {
                        timeUp = Time.time;
                        inputActive = false;
                    }
                }
                break;
        }
    }

    public override void NodeGUI()
    {
        HandleInput();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind key input"))
            {
                binding = true;
            }
        } else
        {
            if (bound)
            {
                GUILayout.Label(string.Format("Bound to \n[ {0} ]", string.Join(" + ", boundKeys.Select(k => k.ToString()))));
                if (GUILayout.Button("Unbind"))
                {
                    bound = false;
                    boundKeys.Clear();
                    bindingKeys.Clear();
                }
            } else
            {
                GUILayout.Label("Press key(s) to bind");
                StringBuilder b = new StringBuilder("");
                if (bindingKeys.Count > 0)
                {
                    b.Append(string.Join(" + ", bindingKeys.Select(k => k.ToString())));
                }
                GUILayout.Label(b.ToString());
            }
        }
        useEasing = RTEditorGUI.Toggle(useEasing, new GUIContent("Use easing", "Apply an easing curve to key input transitions"));
        GUILayout.EndVertical();
        signalOutputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
}