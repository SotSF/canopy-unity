using UnityEngine;
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

    bool showButtons;
    string msg = "";
    float msgDuration = 0;
    float msgSent = 0;

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

    void OnGUI()
    {
        if (Time.time - msgSent < msgDuration)
        {
            GUI.Box(new Rect(Screen.width / 2, Screen.height / 2, Screen.width/4, Screen.height/4), msg);
        }
        if (showButtons)
        {
            for (int i = 1; i < Display.displays.Length; i++)
            {
                if (GUI.Button(new Rect(Screen.width - 120, Screen.height - 120, 120,120), string.Format("Activate Display {0}", i+1)))
                {
                    ActivateDisplay(i);
                    showButtons = false;
                }
            }
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

