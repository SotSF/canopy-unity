using UnityEngine;

public class FPSLimiter : MonoBehaviour
{
    
    private bool lastLimitState = false;
    public bool limitFps = false;

    float lastTargetFps = 30;
    public float targetFps = 30;

    void Start()
    {
        lastLimitState = limitFps;
        lastTargetFps = targetFps;
    }

    // Update is called once per frame
    void Update()
    {
        if (lastLimitState != limitFps || lastTargetFps != targetFps)
        {
            OnChange();
            lastLimitState = limitFps;
            lastTargetFps = targetFps;
        }
    }

    void OnChange()
    {
        Debug.Log("Toggling FPS limit");
        if (limitFps)
        {
            RefreshRate rate = Screen.currentResolution.refreshRateRatio;
            var displayFps = rate.value;
            int syncCount = (int)Mathf.Clamp(Mathf.Floor((float)displayFps/targetFps)+1, 0, 4);
            Debug.Log($"New syncCount: {syncCount}");
            QualitySettings.vSyncCount = syncCount;
        }
        else
        {
            Debug.Log($"SyncCount reset to 1");
            QualitySettings.vSyncCount = 1;
        }
        // QualitySettings.vSyncCount = 0;
        
    }
}
