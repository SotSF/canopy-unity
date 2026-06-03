using UnityEngine;
using System.Collections;
using System;
using UnityEngine.InputSystem;

public class MovementControls: MonoBehaviour
{
    public static MovementControls instance;
    private const float inputThreshold = 0.1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        instance = null;
    }

    // Set by CanopySimulationNode each frame: is the cursor currently over the sim view.
    public bool mouseOverView = false;
    // Latched grab state: a middle-click that began over the view keeps rotating until release.
    private bool rotating = false;

    public float speed = .01f;
    public float mouseSensitivity;

    private float rotationX=0;
    private float rotationY=0;
    private float minimumX = -360;
    private float maximumX = 360;
    private float maximumY = 90;
    private float minimumY = -90;
    private Quaternion originalRotation;
    // Use this for initialization

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        originalRotation = transform.localRotation;
    }

    public void ResetRotation()
    {
        rotationX = 0;
        rotationY = 0;
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f)
            angle += 360f;
        if (angle > 360f)
            angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

// Update is called once per frame
    void Update()
    {

        if (Canopy.instance.simulatorMode)
        {
            // float x = Input.GetAxis("Horizontal") * Time.deltaTime * speed;
            // float z = Input.GetAxis("Vertical") * Time.deltaTime * speed;
            // transform.position += Quaternion.Euler(0,transform.localRotation.eulerAngles.y,0) * new Vector3(x, 0, z);
        }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        // Begin a rotation grab only when the middle button is first pressed while the cursor is
        // over the sim view, then keep rotating until it's released - even if the cursor has since
        // left the node's bounds. Clearing on !isPressed (rather than wasReleasedThisFrame) also
        // ends the grab if the release is missed, e.g. on focus loss.
        if (mouse.middleButton.wasPressedThisFrame && mouseOverView)
            rotating = true;
        else if (!mouse.middleButton.isPressed)
            rotating = false;

        if (rotating)
        {
            rotationX += mouse.delta.x.value * mouseSensitivity;
            rotationY += mouse.delta.y.value * mouseSensitivity;
        }
        // var rawx = Input.GetAxis("RightStickX");
        // var rawy = Input.GetAxis("RightStickY");
        // rotationX += Mathf.Abs(rawx) > inputThreshold ? rawx : 0;
        // rotationY += Mathf.Abs(rawy) > inputThreshold ? rawy : 0;



        if (NodeUIController.instance != null && NodeUIController.instance.inSimulatorMode)
        {
            rotationX = ClampAngle(rotationX, minimumX, maximumX);
            rotationY = ClampAngle(rotationY, minimumY, maximumY);

            Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.up);
            Quaternion yQuaternion = Quaternion.AngleAxis(rotationY, Vector3.left);
            transform.localRotation = originalRotation * xQuaternion * yQuaternion;
        } else
        {
            Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.forward);
            Quaternion yQuaternion = Quaternion.AngleAxis(rotationY, Vector3.right);
            Canopy.instance.UpdateRotation(xQuaternion * yQuaternion);
        }

    }
}
