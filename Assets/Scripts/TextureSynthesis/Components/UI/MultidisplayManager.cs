using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;

public class MultidisplayManager : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        var displayCount = Display.displays.Length;
        if (displayCount > 1)
        {
            showButtons = true;
            ListDisplays();
        }
    }
    public Button displayButton;

    bool showButtons;
    string msg = "";
    float msgDuration = 0;
    float msgSent = 0;

    bool displayActive = false;

    void ShowMessage(string message, float duration)
    {
        msg = message;
        msgDuration = duration;
    }

    void ListDisplays()
    {
        StringBuilder m = new StringBuilder();
        var displayCount = Display.displays.Length;
        for (int i = 0; i < displayCount; i++)
        {
            m.AppendLine("Display " + i.ToString() + ": " + Display.displays[i].ToString());
        }
        ShowMessage(m.ToString(), 4);
    }

    public void ToggleDisplay()
    {
        if (displayActive)
            DeactivateSecondaryDisplay();
        else
            ActivateSecondaryDisplay();
    }

    public void ActivateSecondaryDisplay()
    {
        if (Application.isEditor)
        {
            Camera.main.targetDisplay = 1;
            displayActive = true;
        }
    }

    public void DeactivateSecondaryDisplay()
    {
        if (Application.isEditor)
        {
            Camera.main.targetDisplay = 0;
            displayActive = false;
        }
    }

    public void UpdateAspectRatio(float aspect)
    {
        Camera.main.aspect = aspect;
    }

    public void UpdateFieldOfView(float fov)
    {
        Camera.main.fieldOfView = fov;
    }
    
    void ActivateDisplay(int displayIndex)
    {
        Camera c = Camera.main;
        Display d = Display.displays[displayIndex];
        if (displayIndex >= Display.displays.Length)
            Debug.LogError("Invalid display index");

        int width = Display.displays[displayIndex].systemWidth;
        int height = Display.displays[displayIndex].systemHeight;
        float aspectRatio = ((float)width) / height;
        //if (aspectRatio > 1)
        //{
        //    Camera.main.fieldOfView = 100;
        //}
        //else
        //{
        //    Camera.main.fieldOfView = Mathf.Rad2Deg * 2.0f * Mathf.Atan(Mathf.Tan(100 * 0.5f) / aspectRatio);
        //}
        d.Activate(width, height, 60);
        d.SetParams(width, height, 0, 0);
        c.aspect = aspectRatio;
        c.targetDisplay = displayIndex;
    }
}

