using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;

namespace SecretFire.TextureSynth
{
    public abstract class TextureSynthNode : Node
    {
        protected void KnobOrSlider(ref float val, float min, float max, ValueConnectionKnob knob)
        {
            knob.DisplayLayout();
            if (!knob.connected())
            {
                val = RTEditorGUI.Slider(val, min, max);
            }
        }
    }

    public abstract class TickingNode : TextureSynthNode
    {

    }
}