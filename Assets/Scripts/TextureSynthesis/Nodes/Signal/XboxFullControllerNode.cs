
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using System.Linq;
using UnityEngine;
using XboxCtrlrInput;

[Node(false, "Signal/XboxFullController")]
public class XboxFullControllerNode : TickingNode
{
    public override string GetID => "XboxFullControllerNode";
    public override string Title { get { return "XboxFullController"; } }

    public override Vector2 DefaultSize { get { return new Vector2(150, 400); } }

    // Defines which controller we take data from (?)
    [ValueConnectionKnob("Controller", Direction.In, typeof(int), NodeSide.Left)]
    public ValueConnectionKnob ControllerKnob;


    [ValueConnectionKnob("LeftStick", Direction.Out, typeof(Vector2), NodeSide.Right)]
    public ValueConnectionKnob LeftStickKnob;
    [ValueConnectionKnob("RightStick", Direction.Out, typeof(Vector2), NodeSide.Right)]
    public ValueConnectionKnob RightStickKnob;

    [ValueConnectionKnob("LeftTrigger", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob LeftTriggerKnob;

    [ValueConnectionKnob("RightTrigger", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob RightTriggerKnob;

    [ValueConnectionKnob("dpadUp", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadUpKnob;

    [ValueConnectionKnob("dpadDown", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadDownKnob;

    [ValueConnectionKnob("dpadLeft", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadLeftKnob;

    [ValueConnectionKnob("dpadRight", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob dpadRightKnob;

    [ValueConnectionKnob("a", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob aKnob;

    [ValueConnectionKnob("b", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob bKnob;

    [ValueConnectionKnob("x", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob xKnob;

    [ValueConnectionKnob("y", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob yKnob;

    [ValueConnectionKnob("leftBumper", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob leftBumperKnob;

    [ValueConnectionKnob("rightBumper", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob rightBumperKnob;

    [ValueConnectionKnob("start", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob startKnob;

    [ValueConnectionKnob("back", Direction.Out, typeof(bool), NodeSide.Right)]
    public ValueConnectionKnob backKnob;

    public RadioButtonSet controllerChoice;
    public XboxController boundController;

    private string lastSelected;
    private Vector2 leftStick;
    private Vector2 rightStick;
    private float leftTrigger;
    private float rightTrigger;
    private bool dpadUp, dpadDown, dpadLeft, dpadRight;
    private bool a, b, x, y;
    private bool leftBumper, rightBumper;
    private bool start, back;

    public void Awake()
    {
        if (controllerChoice == null || controllerChoice.names.Count == 0)
        {
            SetControllerRadioButtons();
        }
    }

    private void SetControllerRadioButtons()
    {
        var controllerCount = XCI.GetNumPluggedCtrlrs();
        string[] controllerNames = new string[] { "1st", "2nd", "3rd", "4th" };
        controllerChoice = new RadioButtonSet(0, controllerNames.Take(controllerCount).ToArray());
    }

    private void ChooseController()
    {
        switch (controllerChoice.Selected)
        {
            case "1st":
                boundController = XboxController.First;
                break;
            case "2nd":
                boundController = XboxController.Second;
                break;
            case "3rd":
                boundController = XboxController.Third;
                break;
            case "4th":
                boundController = XboxController.Fourth;
                break;
        }
        lastSelected = controllerChoice.Selected;
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        
        GUILayout.BeginHorizontal();
        RadioButtons(controllerChoice);
        GUILayout.EndHorizontal();

        LeftStickKnob.DisplayLayout();
        RightStickKnob.DisplayLayout();
        LeftTriggerKnob.DisplayLayout();
        RightTriggerKnob.DisplayLayout();
        dpadUpKnob.DisplayLayout();
        dpadDownKnob.DisplayLayout();
        dpadLeftKnob.DisplayLayout();
        dpadRightKnob.DisplayLayout();
        aKnob.DisplayLayout();
        bKnob.DisplayLayout();
        xKnob.DisplayLayout();
        yKnob.DisplayLayout();
        leftBumperKnob.DisplayLayout();
        rightBumperKnob.DisplayLayout();
        startKnob.DisplayLayout();
        backKnob.DisplayLayout();

        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        if (controllerChoice.Selected != lastSelected)
        {
            ChooseController();
        }

        if (XCI.GetNumPluggedCtrlrs() > 0 && XCI.IsPluggedIn(boundController))
        {
            var lX = XCI.GetAxis(XboxAxis.LeftStickX, boundController);
            var lY = XCI.GetAxis(XboxAxis.LeftStickY, boundController);
            var rX = XCI.GetAxis(XboxAxis.RightStickX, boundController);
            var rY = XCI.GetAxis(XboxAxis.RightStickY, boundController);
            leftStick = new Vector2(lX, lY);
            rightStick = new Vector2(rX, rY);

            leftTrigger = XCI.GetAxis(XboxAxis.LeftTrigger, boundController);
            rightTrigger = XCI.GetAxis(XboxAxis.RightTrigger, boundController);

            dpadUp = XCI.GetButton(XboxButton.DPadUp, boundController);
            dpadDown = XCI.GetButton(XboxButton.DPadDown, boundController);
            dpadLeft = XCI.GetButton(XboxButton.DPadLeft, boundController);
            dpadRight = XCI.GetButton(XboxButton.DPadRight, boundController);

            a = XCI.GetButton(XboxButton.A, boundController);
            b = XCI.GetButton(XboxButton.B, boundController);
            x = XCI.GetButton(XboxButton.X, boundController);
            y = XCI.GetButton(XboxButton.Y, boundController);

            leftBumper = XCI.GetButton(XboxButton.LeftBumper, boundController);
            rightBumper = XCI.GetButton(XboxButton.LeftBumper, boundController);

            start = XCI.GetButton(XboxButton.Start, boundController);
            back = XCI.GetButton(XboxButton.Back, boundController);

            LeftStickKnob.SetValue(leftStick);
            RightStickKnob.SetValue(rightStick);
            LeftTriggerKnob.SetValue(leftTrigger);
            RightTriggerKnob.SetValue(rightTrigger);
            dpadUpKnob.SetValue(dpadUp);
            dpadDownKnob.SetValue(dpadDown);
            dpadLeftKnob.SetValue(dpadLeft);
            dpadRightKnob.SetValue(dpadRight);
            aKnob.SetValue(a);
            bKnob.SetValue(b);
            xKnob.SetValue(x);
            yKnob.SetValue(y);
            leftBumperKnob.SetValue(leftBumper);
            rightBumperKnob.SetValue(rightBumper);
            startKnob.SetValue(start);
            backKnob.SetValue(back);
        }
        return true;
    }
}
