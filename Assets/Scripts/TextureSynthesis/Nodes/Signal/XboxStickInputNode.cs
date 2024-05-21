
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using XboxCtrlrInput;

[Node(false, "Signal/XboxStickInput")]
public class XboxStickInputNode : TickingNode
{
    public enum XboxStickId
    {
        left,
        right
    }

    public override string GetID => "XboxStickInputNode";
    public override string Title { get { return "XboxStickInput"; } }

    private Vector2 _DefaultSize = new Vector2(150, 120);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("axis2D", Direction.Out, typeof(Vector2), NodeSide.Right)]
    public ValueConnectionKnob axis2DKnob;
    [ValueConnectionKnob("axisX", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob axisXKnob;
    [ValueConnectionKnob("axisY", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob axisYKnob;

    bool binding = false;
    public bool bound = false;
    public XboxStickId boundStick;
    public XboxController boundController;

    private Vector2 axis2D;

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        if (!bound && !binding)
        {
            if (GUILayout.Button("Bind stick"))
            {
                binding = true;
            }
        }
        else
        {
            if (bound)
            {
                if (GUILayout.Button("Unbind"))
                {
                    bound = false;
                }

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("<{0:0.00},{1:0.00}>", axis2D.x, axis2D.y));
                axis2DKnob.DisplayLayout();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("{0:0.00}", axis2D.x));
                axisXKnob.DisplayLayout();
                GUILayout.EndHorizontal();
                
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("{0:0.00}", axis2D.y));
                axisYKnob.DisplayLayout();
                GUILayout.EndHorizontal();

            }
            else
            {
                GUILayout.Label("Use thumbstick to bind");
            }
        }


        GUILayout.Space(4);
        
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        float epsilon = 0.000001f;

        if (binding)
        {
            var controllerCount = XCI.GetNumPluggedCtrlrs();
            XboxController[] controllers = { XboxController.First, XboxController.Second, XboxController.Third, XboxController.Fourth };
            for (int i = 0; i < controllerCount; i++)
            {
                var controller = controllers[i];

                var leftStickX = XCI.GetAxis(XboxAxis.LeftStickX, controller);
                var leftStickY = XCI.GetAxis(XboxAxis.LeftStickY, controller);
                Vector2 leftStick = new Vector2(leftStickX, leftStickY);
                if (leftStick.magnitude > epsilon)
                {
                    boundController = controller;
                    boundStick = XboxStickId.left;
                    binding = false;
                    bound = true;
                }
                var rightStickX = XCI.GetAxis(XboxAxis.RightStickX, controller);
                var rightStickY = XCI.GetAxis(XboxAxis.RightStickY, controller);
                Vector2 rightStick = new Vector2(rightStickX, rightStickY);
                if (rightStick.magnitude > epsilon)
                {
                    boundController = controller;
                    boundStick = XboxStickId.right;
                    binding = false;
                    bound = true;
                }
            }
        } else if (bound)
        {
            float stickX = 0, stickY = 0;
            switch (boundStick)
            {
                case XboxStickId.left:
                    stickX = XCI.GetAxis(XboxAxis.LeftStickX, boundController);
                    stickY = XCI.GetAxis(XboxAxis.LeftStickY, boundController);
                    break;
                case XboxStickId.right:
                    stickX = XCI.GetAxis(XboxAxis.RightStickX, boundController);
                    stickY = XCI.GetAxis(XboxAxis.RightStickY, boundController);
                    break;
            }
            axis2D = new Vector2(stickX, stickY);
            axis2DKnob.SetValue(axis2D);
            axisXKnob.SetValue(axis2D.x);
            axisYKnob.SetValue(axis2D.y);
        }
        return true;
    }
}
