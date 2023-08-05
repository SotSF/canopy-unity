using UnityEngine;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace SecretFire.TextureSynth
{
    [System.Serializable]
    public class RadioButtonSet
    {
        public List<bool> values;
        public List<string> names;
        public string Selected => SelectedOption();
        // Parameterless constructor to please the XML serialization gods
        public RadioButtonSet(){}

        public RadioButtonSet(params string[] optionNames)
        {
            names = new List<string>(optionNames);
            values = new List<bool>();
            for (int i = 0; i < names.Count; i++)
            {
                values.Add(false);
            }
        }

        public RadioButtonSet(int defaultActive, params string[] optionNames)
        {
            names = new List<string>(optionNames);
            values = new List<bool>();
            for (int i = 0; i < names.Count; i++)
            {
                values.Add(i == defaultActive);
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

        public string SelectedOption()
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i])
                    return names[i];
            }
            return "";
        }
    }

    public abstract class TextureSynthNode : Node
    {
        static GUIStyle _sliderStyle;
        static GUIStyle sliderStyle {
            get {
                if (_sliderStyle == null)
                {
                    _sliderStyle = new GUIStyle();
                    _sliderStyle.fixedWidth = 100;
                    _sliderStyle.normal.textColor = Color.white;
                }
                return _sliderStyle;
            }
        }

        protected void FloatKnobOrSlider(ref float val, float min, float max, ValueConnectionKnob knob, params GUILayoutOption[] layoutOpts)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(5);
            var defaultWidth = GUI.skin.label.fixedWidth;
            GUI.skin.label.fixedWidth = 100;
            knob.DisplayLayout();
            if (!knob.connected())
            {
                val = RTEditorGUI.Slider(val, min, max, layoutOpts);
            } else
            {
                val = knob.GetValue<float>();
            }
            GUI.skin.label.fixedWidth = defaultWidth;
            GUILayout.EndHorizontal();
        }

        protected void FloatKnobOrField(string label, ref float val, ValueConnectionKnob knob, params GUILayoutOption[] layoutOpts)
        {
            GUILayout.BeginHorizontal();
            knob.DisplayLayout();
            if (!knob.connected())
            {
                val = RTEditorGUI.FloatField(label, val, layoutOpts);
            }
            else
            {
                val = knob.GetValue<float>();
            }
            GUILayout.EndHorizontal();
        }

        protected void IntKnobOrSlider(ref int val, int min, int max, ValueConnectionKnob knob, params GUILayoutOption[] layoutOpts)
        {
            GUILayout.BeginHorizontal();
            knob.DisplayLayout();
            if (!knob.connected())
            {
                val = RTEditorGUI.IntSlider(val, min, max, layoutOpts);
            } else
            {
                val = knob.GetValue<int>();
            }
            GUILayout.EndHorizontal();
        }

        protected bool EventKnobOrButtonExclusive(GUIContent label, ValueConnectionKnob knob)
        {
            GUILayout.BeginHorizontal();
            knob.DisplayLayout();
            bool val = knob.connected() ? knob.GetValue<bool>() : GUILayout.Button(label);
            GUILayout.EndHorizontal();
            return val;
        }

        protected bool EventKnobOrButton(string label, ValueConnectionKnob knob)
        {
            GUILayout.BeginHorizontal();
            knob.DisplayLayout();
            bool val = knob.connected() ? GUILayout.Button(label) || knob.GetValue<bool>() : GUILayout.Button(label);
            GUILayout.EndHorizontal();
            return val;
        }

        protected void RadioButtons(RadioButtonSet buttons)
        {
            for (int i = 0; i < buttons.names.Count; i++)
            {
                if (RTEditorGUI.Toggle(buttons.values[i], buttons.names[i]))
                {
                    buttons.SelectOption(i);
                }
                else
                {
                    buttons.values[i] = false;
                }
            }
        }

        protected void RadioButtonsHorizontal(RadioButtonSet buttons)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < buttons.names.Count; i++)
            {
                if (RTEditorGUI.Toggle(buttons.values[i], buttons.names[i]))
                {
                    buttons.SelectOption(i);
                }
                else
                {
                    buttons.values[i] = false;
                }
            }
            GUILayout.EndHorizontal();
        }

        protected void RadioButtonsVertical(RadioButtonSet buttons)
        {
            GUILayout.BeginVertical();
            for (int i = 0; i < buttons.names.Count; i++)
            {
                if (RTEditorGUI.Toggle(buttons.values[i], buttons.names[i]))
                {
                    buttons.SelectOption(i);
                }
                else
                {
                    buttons.values[i] = false;
                }
            }
            GUILayout.EndVertical();
        }
    }

    public abstract class TickingNode : TextureSynthNode
    {

    }

    public class GraphProvider {
        private List<float> timeValues;
        private List<float> signalValues;

        private int gridPointsKernel;
        private int horizontalAxisKernel;
        private int verticalAxisKernel;
        private int graphKernel;

        private ComputeShader graphShader;
        public RenderTexture graphTexture;

        private Vector2Int outputSize;
        
        public GraphProvider(){
            InitializeValues(new Vector2Int(64, 64));
        }

        public GraphProvider(Vector2Int outputSize){
            InitializeValues(outputSize);
        }

        private void InitializeRenderTexture()
        {
            graphTexture = new RenderTexture(outputSize.x, outputSize.y, 0);
            graphTexture.enableRandomWrite = true;
            graphTexture.Create();
            RenderTexture.active = graphTexture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = null;
        }

        private void InitializeValues(Vector2Int outputSize){
            timeValues = new List<float>(257);
            signalValues = new List<float>(257);
            graphShader = Resources.Load<ComputeShader>("NodeShaders/GraphView");
            gridPointsKernel = graphShader.FindKernel("gridPoints");
            horizontalAxisKernel = graphShader.FindKernel("horizontalAxis");
            verticalAxisKernel = graphShader.FindKernel("verticalAxis");
            graphKernel = graphShader.FindKernel("graph");
            this.outputSize = outputSize;
            InitializeRenderTexture();
        }


        public void AddDatapoint(float x, float y){
            timeValues.Add(x);
            signalValues.Add(y);
            if (timeValues.Count > 255){
                timeValues.RemoveAt(0);
                signalValues.RemoveAt(0);
            }
        }

        public void AddDatapoint(float value){
            AddDatapoint(Time.time, value);
        }

        public void DrawGraph()
        {
            float windowMaxX = 1, windowMinX = -1, windowMaxY = 1, windowMinY = -1;

            graphShader.SetInt("minTickSpacing", 5);
            graphShader.SetInts("texSize", outputSize.x, outputSize.y);
            graphShader.SetFloats("xValues", timeValues.ToArray());
            graphShader.SetFloats("yValues", signalValues.ToArray());
            graphShader.SetInt("numPoints", timeValues.Count);

            if (timeValues.Count > 0 && signalValues.Count > 0)
            {
                var minX = timeValues.Min();
                var maxX = timeValues.Max();
                var minY = signalValues.Min();
                var maxY = signalValues.Max();
                windowMinX = minX - (maxX - minX) / 20;
                windowMaxX = maxX + (maxX - minX) / 20;
                windowMinY = minY - (maxY - minY) / 20;
                windowMaxY = maxY + (maxY - minY) / 20;
                graphShader.SetFloats("windowMin", windowMinX, windowMinY);
                graphShader.SetFloats("windowMax", windowMaxX, windowMaxY);
            }

            // Set colors
            graphShader.SetVector("lineColor", Color.cyan);
            graphShader.SetVector("backgroundColor", new Color(0.1f, 0.1f, 0.1f, 1));
            graphShader.SetVector("labelColor", Color.white);

            // Set render texture
            graphShader.SetTexture(graphKernel, "outputTex", graphTexture);
            graphShader.SetTexture(gridPointsKernel, "outputTex", graphTexture);
            graphShader.SetTexture(verticalAxisKernel, "outputTex", graphTexture);
            graphShader.SetTexture(horizontalAxisKernel, "outputTex", graphTexture);

            // Dispatch kernels
            uint tx, ty, tz;
            graphShader.GetKernelThreadGroupSizes(gridPointsKernel, out tx, out ty, out tz);
            var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
            var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);

            graphShader.Dispatch(gridPointsKernel, threadGroupX, threadGroupY, 1);
            
            if (windowMinY < 0 && windowMaxY > 0)
            {
                graphShader.Dispatch(horizontalAxisKernel, Mathf.CeilToInt(outputSize.x / 256f), 1, 1);
            }
            if (windowMinX < 0 && windowMaxX > 0)
            {
                graphShader.Dispatch(verticalAxisKernel, 1, Mathf.CeilToInt(outputSize.y / 256f), 1);
            }
            if (signalValues.Count > 0)
            {
                graphShader.Dispatch(graphKernel, 1, 1, 1);
            }
        }
    }
}