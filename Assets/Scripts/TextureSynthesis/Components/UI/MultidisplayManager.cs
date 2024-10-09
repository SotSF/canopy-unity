using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;
using UnityEditor;
using System.Collections.Generic;

public class MultidisplayManager : MonoBehaviour
{
    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        var displayCount = Display.displays.Length;
        if (displayCount > 1)
        {
            ListDisplays();
        }
    }
    public Button displayButton;
    public static MultidisplayManager instance;   
    public Camera MainCamera;
    public Camera BlankCamera;
    public Text textTag;


    bool displayActive = false;


    void ListDisplays()
    {
        StringBuilder m = new StringBuilder();
        var displayCount = Display.displays.Length;
        for (int i = 0; i < displayCount; i++)
        {
            m.AppendLine("Display " + i.ToString() + ": " + Display.displays[i].ToString());
        }

    }

    internal Vector2 GetEditorGameWindowSize(int index)
    {
#if UNITY_EDITOR
        {
            System.Reflection.Assembly assembly = typeof(EditorWindow).Assembly;
            System.Type GameView = assembly.GetType("UnityEditor.GameView");
            var gameWindow = EditorWindow.GetWindow(GameView);
            var GetSizeMethod = GameView.GetMethod("GetPlayModeViewSize", System.Reflection.BindingFlags.NonPublic);
            var ViewArrayInfo = GameView.GetField("s_PlayModeViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            //var ViewArray = ViewArrayInfo.GetValue(null) as List<object>;
            //var window = ViewArray[index];
            //System.Object Res = GetSizeMethod.Invoke(null, null);
            return new Vector2(gameWindow.position.width,gameWindow.position.height);
        }
#endif
#pragma warning disable CS0162 // Unreachable code detected - is reachable due to pragma
        return Vector2.zero;
#pragma warning restore CS0162 // Unreachable code detected
    }

    internal Vector2 GetDisplaySize(int displayIndex)
    {
        if (Application.isEditor)
        {
            return GetEditorGameWindowSize(displayIndex);
        }
        if (displayIndex >= Display.displays.Length)
        {
            Debug.LogError("Invalid display index");
            return Vector2.zero;
        }
        Display d = Display.displays[displayIndex];

        int width = d.systemWidth;
        int height = d.systemHeight;
        return new Vector2(width, height);
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
            BlankCamera.targetDisplay = 0;
            MainCamera.targetDisplay = 1;
            displayActive = true;
            textTag.text = "Deactivate 2nd display";
        } else
        {
            ActivateDisplay(1);
        }
    }

    public void DeactivateSecondaryDisplay()
    {
        if (Application.isEditor)
        {
            MainCamera.targetDisplay = 0;
            BlankCamera.targetDisplay = 1;
            displayActive = false;
            textTag.text = "Activate 2nd display";
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
    
    public void ActivateDisplay(int displayIndex)
    {
        if (Application.isEditor)
            return;
        if (displayIndex >= Display.displays.Length)
        {
            Debug.LogError("Invalid display index");
            return;
        }
        Display d = Display.displays[displayIndex];

        int width = d.systemWidth;
        int height = d.systemHeight;
        float aspectRatio = ((float)width) / height;
        //if (aspectRatio > 1)
        //{
        //    Camera.main.fieldOfView = 100;
        //}
        //else
        //{
        //    Camera.main.fieldOfView = Mathf.Rad2Deg * 2.0f * Mathf.Atan(Mathf.Tan(100 * 0.5f) / aspectRatio);
        //}
        d.Activate();
        //d.Activate(width, height, 60);
        //d.SetParams(width, height, 0, 0);
        //c.aspect = aspectRatio;
        //c.targetDisplay = displayIndex;
    }
}

