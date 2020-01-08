using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using System.Collections.Generic;

namespace SecretFire.TextureSynth
{
    public abstract class TextureSynthNode : Node
    {
        protected void FloatKnobOrSlider(ref float val, float min, float max, ValueConnectionKnob knob)
        {
            knob.DisplayLayout();
            if (!knob.connected())
            {
                val = RTEditorGUI.Slider(val, min, max);
            }
        }

        protected bool EventKnobOrButtonExclusive(GUIContent label, ValueConnectionKnob knob)
        {
            knob.DisplayLayout();
            if (!knob.connected())
            {
                return GUILayout.Button(label);
            } else
            {
                return knob.GetValue<bool>();
            }
        }

        protected Dictionary<string, bool> RadioButtons(Dictionary<string, bool> buttons)
        {
            List<string> keys = new List<string>(buttons.Keys);
            foreach (var name in keys)
            {
                if (RTEditorGUI.Toggle(buttons[name], name))
                {
                    foreach (var subname in keys)
                    {
                        // Set all values except the selected one to false
                        buttons[subname] = subname == name;
                    }
                } else
                {
                    buttons[name] = false;
                }
            }
            return buttons;
        }

        protected bool EventKnobOrButton(string label, ValueConnectionKnob knob)
        {
            knob.DisplayLayout();
            if (!knob.connected())
            {
                return GUILayout.Button(label);
            }
            else
            {
                return GUILayout.Button(label) || knob.GetValue<bool>();
            }
        }
    }

    public abstract class TickingNode : TextureSynthNode
    {

    }
}