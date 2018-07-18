using UnityEngine;
using System.Collections;
using System;

public class Controls: MonoBehaviour
{
    public PatternManager manager;
    private const float inputThreshold = 0.1f;
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
    void Start()
    {
        originalRotation = transform.localRotation;
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

        float x = 0f;//Input.GetAxis("Horizontal") * Time.deltaTime * speed;
        float z = 0f;// Input.GetAxis("Vertical") * Time.deltaTime * speed;
        transform.position += Quaternion.Euler(0,transform.localRotation.eulerAngles.y,0) * new Vector3(x, 0, z);

        //If mouse mode?
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            rotationY += Input.GetAxis("Mouse Y") * mouseSensitivity;
        }
        var rawx = Input.GetAxis("RightStickX");
        var rawy = Input.GetAxis("RightStickY");
        rotationX += Mathf.Abs(rawx) > inputThreshold ? rawx : 0;
        rotationY += Mathf.Abs(rawy) > inputThreshold ? rawy : 0;

        rotationX = ClampAngle(rotationX, minimumX, maximumX);
        rotationY = ClampAngle(rotationY, minimumY, maximumY);

        Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.up);
        Quaternion yQuaternion = Quaternion.AngleAxis(rotationY, Vector3.left);

        transform.localRotation = originalRotation * xQuaternion * yQuaternion;
        CheckButtons();
    }

    private void CheckButtons()
    {
        var aIndex = KeyCode.Joystick1Button0;
        var bIndex = KeyCode.Joystick1Button1;
        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            aIndex = KeyCode.Joystick1Button0;
            bIndex = KeyCode.Joystick1Button1;
        } else if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            aIndex = KeyCode.Joystick1Button16;
            bIndex = KeyCode.Joystick1Button17;
        }
        
        if (Input.GetKeyDown(aIndex))
        {
            // Pressed 'A' on controller
            manager.NextPattern();
        }
        if (Input.GetKeyDown(bIndex))
        {
            manager.PreviousPattern();
        }
    }
}
