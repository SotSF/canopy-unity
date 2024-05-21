using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

[Node(false, "Signal/KeySignal")]
public class KeySignalNode : TickingNode
{
    public override string GetID => "KeySignal";
    public override string Title { get { return "KeySignal"; } }
    private Vector2 _DefaultSize = new Vector2(150, 100);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Out", Direction.Out, typeof(float))]
    public ValueConnectionKnob signalOutputKnob;

    [ValueConnectionKnob("pressed", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob pressedKnob;
    bool pressed;

    [ValueConnectionKnob("held", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob heldKnob;
    bool held;

    [ValueConnectionKnob("released", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob releasedKnob;
    bool released;

    public HashSet<KeyCode> bindingKeys;
    public HashSet<KeyCode> boundKeys;
    bool inputActive = false;

    bool useEasing = false;
    bool binding = false;
    public bool bound = false;

    float timeDown = 0;
    float timeUp = 0;

    private void Awake()
    {
        if (bindingKeys == null){
            bindingKeys = new HashSet<KeyCode>();
        }
        if (boundKeys == null)
        {
            boundKeys = new HashSet<KeyCode>();
        }
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
        pressedKnob.SetValue<bool>(pressed);
        if (pressed){
            pressed = false;
        }
        heldKnob.SetValue<bool>(held);
        releasedKnob.SetValue<bool>(released);
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
                        pressed = true;
                        held = true;
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
                        held = false;
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
        GUILayout.BeginVertical();
        signalOutputKnob.DisplayLayout();
        pressedKnob.DisplayLayout();
        heldKnob.DisplayLayout();
        releasedKnob.DisplayLayout();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
}