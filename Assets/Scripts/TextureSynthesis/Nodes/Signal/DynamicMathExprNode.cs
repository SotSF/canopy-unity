
using DynamicExpresso;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

[Node(false, "Signal/DynMathExpr")]
public class DynamicMathExprNode : Node
{
    public override string GetID => "DynMathExprNode";
    public override string Title { get { return "DynMathExpr"; } }
    public override bool AutoLayout => true;
    public override Vector2 DefaultSize => new Vector2(160, (1 + targetPortCount) * 120);
    public override Vector2 MinSize => new Vector2(160, 120);

    [ValueConnectionKnob("output", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputKnob;

    private string lastexpr = "";
    public string stringexpr;

    private float output;
    private Interpreter interpreter;
    private Lambda exprFunc;
    private string errorMsg = "";

    private int targetPortCount => activePortCount + 1;
    private IEnumerable<ConnectionPort> connectedPorts => dynamicConnectionPorts.Where(port => port.connected());
    private int activePortCount => connectedPorts.Count();
    private int openPortIndex => activePortCount;

    private string alpha = "abcdefghijklmnopqrstuvwxyz";

    public List<Parameter> exprParams;

    private void Awake()
    {

        interpreter = new Interpreter();
        MathWrapper.SetInterpreterEnv(interpreter);
        if (exprParams == null)
        {
            exprParams = new List<Parameter>()
            {
                new Parameter("a", typeof(float)),
                new Parameter("b", typeof(float)),
                new Parameter("c", typeof(float)),
                new Parameter("d", typeof(float)),
                new Parameter("e", typeof(float)),
                new Parameter("f", typeof(float)),
                new Parameter("g", typeof(float)),
                new Parameter("h", typeof(float)),
                new Parameter("i", typeof(float)),
                new Parameter("j", typeof(float)),
                new Parameter("k", typeof(float)),
                new Parameter("l", typeof(float)),
                new Parameter("m", typeof(float)),
                new Parameter("n", typeof(float)),
                new Parameter("o", typeof(float)),
                new Parameter("p", typeof(float)),
                new Parameter("q", typeof(float)),
                new Parameter("r", typeof(float)),
                new Parameter("s", typeof(float)),
                new Parameter("t", typeof(float)),
                new Parameter("u", typeof(float)),
                new Parameter("v", typeof(float)),
                new Parameter("w", typeof(float)),
                new Parameter("x", typeof(float)),
                new Parameter("y", typeof(float)),
                new Parameter("z", typeof(float)),
            };
        }
        if (stringexpr != null)
        {
            Parse();
        }
    }

    private void SetPortCount()
    {
        // Keep one open slot at the bottom of the input list
        // Adjust the active signal index if necessary
        if (dynamicConnectionPorts.Count > targetPortCount)
        {
            for (int i = 0; i < dynamicConnectionPorts.Count - 1; i++)
            {
                var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
                if (!port.connected())
                {
                    DeleteConnectionPort(i);
                    Parse();
                }
            }
        }
        else if (dynamicConnectionPorts.Count < targetPortCount && targetPortCount < 27)
        {
            ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute("Add input", Direction.In, typeof(float), NodeSide.Left);
            while (dynamicConnectionPorts.Count < targetPortCount)
            {
                CreateValueConnectionKnob(outKnobAttribs);
                Parse();
            }
        }
    }

    public override void NodeGUI()
    {
        SetPortCount();
        GUILayout.BeginVertical();
        stringexpr = RTEditorGUI.TextField(stringexpr);
        if (stringexpr != lastexpr)
        {
            lastexpr = stringexpr;
            Parse();
        }
        //Knob display

        for (int i = 0; i < targetPortCount-1; i++)
        {
            GUILayout.BeginHorizontal();
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            GUILayout.Label(string.Format("Input {0}: {1}", alpha[i], port.GetValue<float>()));
            port.SetPosition();
            GUILayout.EndHorizontal();
        }
        ((ValueConnectionKnob)dynamicConnectionPorts[openPortIndex]).DisplayLayout();

        if (errorMsg != null && errorMsg != "")
            GUILayout.Label(string.Format("Error: {0}", errorMsg));
        else
            GUILayout.Label(string.Format("Result: {0:0.000}", output));
        outputKnob.DisplayLayout();

        GUILayout.EndVertical();


        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void Parse()
    {
        if (stringexpr != null && stringexpr != "")
        {
            try
            {
                exprFunc = interpreter.Parse(
                    stringexpr, 
                    exprParams.GetRange(0, activePortCount).ToArray()
                );
                Calculate();
            }
            catch (Exception e)
            {
                errorMsg = "<Parse> - " + e.Message;
                exprFunc = null;
            }
        }
    }

    public static class MathWrapper{
        public delegate float UnaryMathFunc(float a);
        public delegate float BinaryMathFunc(float a, float b);
        public delegate float TrinaryMathFunc(float a, float b, float c);
        
        public static void SetInterpreterEnv(Interpreter interp)
        {
            interp.SetFunction("abs", (UnaryMathFunc)Mathf.Abs);
            interp.SetFunction("acos", (UnaryMathFunc)Mathf.Acos);
            interp.SetFunction("asin", (UnaryMathFunc)Mathf.Asin);
            interp.SetFunction("atan2", (BinaryMathFunc)Mathf.Atan2);
            interp.SetFunction("clamp", (TrinaryMathFunc)Mathf.Clamp);
            interp.SetFunction("cos", (UnaryMathFunc)Mathf.Cos);
            interp.SetFunction("exp", (UnaryMathFunc)Mathf.Exp);
            interp.SetFunction("invlerp", (TrinaryMathFunc)Mathf.InverseLerp);
            interp.SetFunction("log", (BinaryMathFunc)Mathf.Log);
            interp.SetFunction("log10", (UnaryMathFunc)Mathf.Log10);
            interp.SetFunction("lerp", (TrinaryMathFunc)Mathf.Lerp);
            interp.SetFunction("lerpangle", (TrinaryMathFunc)Mathf.LerpAngle);
            interp.SetFunction("max", (BinaryMathFunc)Mathf.Max);
            interp.SetFunction("min", (BinaryMathFunc)Mathf.Min);
            interp.SetFunction("pow", (BinaryMathFunc)Mathf.Pow);
            interp.SetFunction("sin", (UnaryMathFunc)Mathf.Sin);
            interp.SetFunction("sqrt", (UnaryMathFunc)Mathf.Sqrt);
            interp.SetFunction("tan", (UnaryMathFunc)Mathf.Tan);
            interp.SetVariable("pi", Mathf.PI);
            interp.SetVariable("rad2deg", Mathf.Rad2Deg);
            interp.SetVariable("deg2rad", Mathf.Deg2Rad);
        }
    }


    public override bool Calculate()
    {
        if (exprFunc != null)
        {
            try
            {
                var paramValues = connectedPorts.Select((port, i) => ((ValueConnectionKnob)port).GetValue<float>()).ToArray();
                output = float.Parse(exprFunc.Invoke(paramValues.Cast<object>().ToArray()).ToString());
                if (errorMsg != "")
                    errorMsg = "";
            }
            catch (Exception e)
            {
                errorMsg = "<Execute> - " + e.Message;
            }
        }
        outputKnob.SetValue(output);
        return true;
    }
}
