
using DynamicExpresso;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Float/MathExpr")]
public class MathExprNode : Node
{
    public override string GetID => "MathExprNode";
    public override string Title { get { return "MathExpr"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 100); } }

    [ValueConnectionKnob("a", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob aKnob;
    [ValueConnectionKnob("b", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob bKnob;

    [ValueConnectionKnob("output", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob outputKnob;

    public string expr;

    private float a, b;
    private float output;
    private Interpreter interpreter;

    private void Awake()
    {
        interpreter = new Interpreter();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        expr = RTEditorGUI.TextField(expr);

        //Knob display
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        aKnob.DisplayLayout();
        bKnob.DisplayLayout();
        GUILayout.EndVertical();
        outputKnob.DisplayLayout();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();


        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public struct MathWrapper{
        public float Lerp(float a, float b, float t)
        {
            return Mathf.Lerp(a, b, t);
        }
    }

    public override bool Calculate()
    {
        if (expr != null && expr != "")
        {
            try
            {
                interpreter.SetVariable("Mathf", new MathWrapper());
                interpreter.SetVariable("a", aKnob.GetValue<float>());
                interpreter.SetVariable("b", bKnob.GetValue<float>());
                output = interpreter.Eval<float>(expr);
            }
            catch {
                // do nothing, this is normal when typing in an expression
                //Debug.Log("Bad expr in MathExpr node");
            }
        }
        outputKnob.SetValue(output);
        return true;
    }
}
