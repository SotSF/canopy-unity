
using DynamicExpresso;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System;
using UnityEngine;

[Node(false, "Signal/MathExpr")]
public class MathExprNode : Node
{
    public override string GetID => "MathExprNode";
    public override string Title { get { return "MathExpr"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    [ValueConnectionKnob("a", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob aKnob;
    [ValueConnectionKnob("b", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob bKnob;
    [ValueConnectionKnob("c", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob cKnob;

    [ValueConnectionKnob("output", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputKnob;

    private string lastexpr = "";
    public string stringexpr;

    private float a, b;
    private float output;
    private Interpreter interpreter;
    private Lambda exprFunc;
    private string errorMsg = "";

    private void Awake()
    {
        interpreter = new Interpreter();
        MathWrapper.SetInterpreterEnv(interpreter);
        if (stringexpr != null)
        {
            Parse();
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        stringexpr = RTEditorGUI.TextField(stringexpr);
        if (stringexpr != lastexpr)
        {
            lastexpr = stringexpr;
            Parse();
        }
        //Knob display
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        aKnob.DisplayLayout();
        bKnob.DisplayLayout();
        GUILayout.EndVertical();
        if (errorMsg != null && errorMsg != "")
            GUILayout.Label(string.Format("Error: {0}", errorMsg));
        else
            GUILayout.Label(string.Format("Result: {0:0.000}", output));
        outputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

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
                var parameters = new[] { new Parameter("a", typeof(float)),
                                         new Parameter("b", typeof(float)),
                                         new Parameter("c", typeof(float))};
                exprFunc = interpreter.Parse(stringexpr, parameters);
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
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
                output = float.Parse(exprFunc.Invoke(aKnob.GetValue<float>(), bKnob.GetValue<float>(), cKnob.GetValue<float>()).ToString());
                if (errorMsg != "")
                    errorMsg = "";
            } catch (Exception e)
            {
                errorMsg = e.Message;
            }
        }
        outputKnob.SetValue(output);
        return true;
    }
}
