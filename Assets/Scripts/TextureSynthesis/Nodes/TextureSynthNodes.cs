using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using System.Collections.Generic;
using System;

namespace SecretFire.TextureSynth
{
    [Serializable]
    public class RadioButtonSet
    {
        public List<bool> values;
        public List<string> names;

        public RadioButtonSet(params string[] optionNames)
        {
            names = new List<string>(optionNames);
            values = new List<bool>();
            for (int i = 0; i < names.Count; i++)
            {
                values.Add(false);
            }
        }

        public bool IsSelected(string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                {
                    return values[i];
                }
            }
            return false;
        }

        public void SelectOption(int index)
        {
            for (int i = 0; i < values.Count; i++)
            {
                values[i] = (i == index);
            }
        }
    }

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

        protected void RadioButtons(RadioButtonSet buttons)
        {
            for (int i = 0; i < buttons.names.Count; i++)
            {
                if (RTEditorGUI.Toggle(buttons.values[i], buttons.names[i]))
                {
                    buttons.SelectOption(i);
                } else
                {
                    buttons.values[i] = false;
                }
            }
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